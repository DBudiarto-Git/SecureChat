using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SecureChat.Core;
using SecureChat.Infrastructure;
using SecureChat.WebAPI.Hubs;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SecureChat.WebAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class FriendshipController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<ChatHub> _hubContext;

    public FriendshipController(ApplicationDbContext context, IHubContext<ChatHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    // 1. ANFRAGE SENDEN: api/friendship/invite
    [HttpPost("invite")]
    public async Task<IActionResult> SendInvite([FromBody] string receiverEmail)
    {
        var senderEmail = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(senderEmail)) return Unauthorized();

        if (senderEmail.Equals(receiverEmail, StringComparison.OrdinalIgnoreCase))
            return BadRequest("Du kannst dir selbst keine Freundschaftsanfrage senden.");

        // 1. KORREKTUR: Wir suchen den Empfänger in der Identity-Tabelle anhand der E-Mail, um seine echte UserId (GUID) zu bekommen!
        var receiverUser = await _context.Set<IdentityUser>()
            .FirstOrDefaultAsync(u => u.Email == receiverEmail);

        if (receiverUser == null)
            return NotFound("Der gesuchte Benutzer existiert nicht.");

        // Die echte GUID des Empfängers für die Freundschafts-Tabelle nutzen
        string receiverId = receiverUser.Id;

        // Die eigene GUID des Senders ermitteln (da SenderId im Token meistens die ID ist)
        var senderUser = await _context.Set<IdentityUser>()
            .FirstOrDefaultAsync(u => u.Email == senderEmail);
        string senderId = senderUser?.Id ?? senderEmail;

        // 2. Prüfen, ob bereits eine Beziehung existiert (jetzt über die echten IDs)
        var existingFriendship = await _context.Friendships
            .AnyAsync(f => (f.SenderId == senderId && f.ReceiverId == receiverId) ||
                           (f.SenderId == receiverId && f.ReceiverId == senderId));

        if (existingFriendship)
            return BadRequest("Es existiert bereits eine Anfrage oder Freundschaft mit diesem Benutzer.");

        // 3. Freundschaftseintrag mit den korrekten GUIDs speichern
        var friendship = new Friendship
        {
            SenderId = senderId,
            ReceiverId = receiverId,
            Status = FriendshipStatus.Pending
        };

        _context.Friendships.Add(friendship);
        await _context.SaveChangesAsync();

        // ECHTZEIT-NOTIFIKATION FÜR DEN EMPFÄNGER DER ANFRAGE
        // Wir nutzen den injizierten IHubContext<ChatHub> (stelle sicher, dass _hubContext im Controller verfügbar ist)
        if (ChatHub.OnlineUsers.TryGetValue(receiverEmail, out var connectionId))
        {
            // Wir funken das neue Event "NewFriendInviteReceived" live an den PC des Empfängers
            await _hubContext.Clients.Client(connectionId).SendAsync("NewFriendInviteReceived");
        }

        return Ok(new { Message = "Freundschaftsanfrage erfolgreich gesendet!" });
    }


    // 2. ANFRAGE ANNEHMEN/ABLEHNEN: api/friendship/respond
    public record RespondDto(int FriendshipId, bool Accept);

    [HttpPost("respond")]
    public async Task<IActionResult> RespondToInvite([FromBody] RespondDto dto)
    {
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var friendship = await _context.Friendships.FindAsync(dto.FriendshipId);
        if (friendship == null) return NotFound("Anfrage nicht gefunden.");

        // SECURITY: Nur der echte Empfänger darf die Anfrage annehmen oder ablehnen!
        if (friendship.ReceiverId != currentUserId)
            return Forbid("Du bist nicht berechtigt, auf diese Anfrage zu antworten.");

        if (friendship.Status != FriendshipStatus.Pending)
            return BadRequest("Diese Anfrage wurde bereits bearbeitet.");

        friendship.Status = dto.Accept ? FriendshipStatus.Accepted : FriendshipStatus.Rejected;
        friendship.ActionAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(new { Message = dto.Accept ? "Freundschaft erfolgreich geschlossen!" : "Anfrage abgelehnt." });
    }

    // 3. MEINE BESTÄTIGTEN FREUNDE ABRUFEN: api/friendship/friends
    //[HttpGet("friends")]
    //public async Task<IActionResult> GetMyFriends()
    //{
    //    var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    //    if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

    //    var friendships = await _context.Friendships
    //        .Where(f => (f.SenderId == currentUserId || f.ReceiverId == currentUserId) && f.Status == FriendshipStatus.Accepted)
    //        .ToListAsync();

    //    var friendIds = friendships.Select(f => f.SenderId == currentUserId ? f.ReceiverId : f.SenderId).ToList();

    //    var friendsList = await _context.UserProfiles
    //        .Where(p => friendIds.Contains(p.Id))
    //        .Join(_context.Set<IdentityUser>(),
    //            profile => profile.Id,
    //            user => user.Id,
    //            (profile, user) => new
    //            {
    //                Email = user.Email,
    //                DisplayName = profile.DisplayName,
    //                IsActive = profile.IsActive,
    //                ProfilePictureBase64 = profile.ProfilePictureUrl ?? string.Empty // <-- BILD MITÜBERGEBEN!
    //            })
    //        .ToListAsync();

    //    return Ok(friendsList);
    //}

    [HttpGet("friends")]
    public async Task<IActionResult> GetMyFriends()
    {
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

        var friendships = await _context.Friendships
            .AsNoTracking() // Boostet die Performance beim reinen Lesen
            .Where(f => (f.SenderId == currentUserId || f.ReceiverId == currentUserId) && f.Status == FriendshipStatus.Accepted)
            .ToListAsync();

        var friendIds = friendships.Select(f => f.SenderId == currentUserId ? f.ReceiverId : f.SenderId).ToList();

        var friendsList = await _context.UserProfiles
            .AsNoTracking() // Deaktiviert das EF-Change-Tracking für maximale Geschwindigkeit
            .Where(p => friendIds.Contains(p.Id))
            .Join(_context.Set<IdentityUser>(),
                profile => profile.Id,
                user => user.Id,
                (profile, user) => new
                {
                    Email = user.Email,
                    DisplayName = profile.DisplayName,
                    IsActive = profile.IsActive,
                    ProfilePictureBase64 = profile.ProfilePictureUrl ?? string.Empty, // <-- BILD MITÜBERGEBEN!

                    // 🌟 DIE KRÖNUNG FÜR HEUTE: Der integrierte Live-Zähler!
                    // Zählt alle ungelesenen Nachrichten, die genau DIESER Freund an MICH gesendet hat
                    UnreadCount = _context.ChatMessages.Count(m => m.SenderId == profile.Id && m.ReceiverId == currentUserId && m.MessageStatus < 2)
                })
            .ToListAsync();

        return Ok(friendsList);
    }

    // Endpunkt: api/friendship/pending
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingInvites()
    {
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

        // Wir suchen alle Zeilen, in denen der aktuelle User der Empfänger ist und der Status wartet (Pending)
        var pendingInvites = await _context.Friendships
            .Where(f => f.ReceiverId == currentUserId && f.Status == FriendshipStatus.Pending)
            .Join(_context.Set<IdentityUser>(),
                friendship => friendship.SenderId,
                user => user.Id,
                (friendship, user) => new
                {
                    FriendshipId = friendship.Id,
                    SenderEmail = user.Email,
                    // Wir holen das Profil für den Anzeigenamen dazu
                    SenderDisplayName = _context.UserProfiles
                        .Where(p => p.Id == friendship.SenderId)
                        .Select(p => p.DisplayName)
                        .FirstOrDefault() ?? user.Email
                })
            .ToListAsync();

        return Ok(pendingInvites);
    }

}


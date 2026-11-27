using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureChat.Core;
using SecureChat.Infrastructure;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SecureChat.WebAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public UsersController(ApplicationDbContext context)
    {
        _context = context;
    }

    // 1. EIGENEN RSA-SCHLÜSSEL HOCHLADEN/AKTUALISIEREN: api/users/keys
    public record UpdateKeysDto(string PublicKeyXml);

    [HttpPost("keys")]
    public async Task<IActionResult> UpdatePublicKey([FromBody] UpdateKeysDto dto)
    {
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.PublicKeyXml))
            return BadRequest("Ungültiger öffentlicher Schlüssel.");

        var profile = await _context.UserProfiles.FindAsync(currentUserId);
        if (profile == null) return NotFound("Benutzerprofil nicht gefunden.");

        // Neuen Public-Key in die SQL-Datenbank brennen
        profile.RsaPublicKeyXml = dto.PublicKeyXml;
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Öffentlicher RSA-Schlüssel erfolgreich im SQL Server rotiert." });
    }

    // 2. DEN RSA-SCHLÜSSEL EINES GEGENSPRECHERS ABRUFEN: api/users/publickey?email=alice@secure.com
    [HttpGet("publickey")]
    public async Task<IActionResult> GetFriendPublicKey([FromQuery] string email)
    {
        var user = await _context.Set<IdentityUser>()
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null) return NotFound("Benutzer nicht gefunden.");

        var profile = await _context.UserProfiles.FindAsync(user.Id);
        if (profile == null || string.IsNullOrEmpty(profile.RsaPublicKeyXml))
            return NotFound("Der Benutzer hat noch keinen krypto-sicheren Schlüssel generiert.");

        return Ok(new { PublicKeyXml = profile.RsaPublicKeyXml });
    }

    // 3. ALLE NUTZER ABRUFEN
    [HttpGet]
    public async Task<IActionResult> GetAllUsers()
    {
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var identityUsers = _context.Set<IdentityUser>();

        var users = await _context.UserProfiles
            .Where(p => p.Id != currentUserId && p.IsActive)
            .Join(identityUsers,
                profile => profile.Id,
                user => user.Id,
                (profile, user) => new
                {
                    Email = user.Email,
                    DisplayName = profile.DisplayName,
                    IsActive = profile.IsActive
                })
            .ToListAsync();

        return Ok(users);
    }

    // 4. LIVE-SUCHE
    [HttpGet("search")]
    public async Task<IActionResult> SearchUsers([FromQuery] string query, [FromQuery] int? groupId = null)
    {
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(query)) return BadRequest("Suchbegriff leer.");

        var identityUsers = _context.Set<IdentityUser>();

        // 1. Alle passenden Benutzer laden (außer uns selbst)
        var users = await _context.UserProfiles
            .Where(p => p.Id != currentUserId && p.IsActive && p.DisplayName.Contains(query))
            .Join(identityUsers,
                profile => profile.Id,
                user => user.Id,
                (profile, user) => new { profile.Id, Email = user.Email, DisplayName = profile.DisplayName, IsActive = profile.IsActive })
            .Take(15)
            .ToListAsync();

        var resultList = new List<object>();
        foreach (var u in users)
        {
            // A) Prüfen, ob bereits eine Freundschaft existiert (oder angefragt ist)
            bool isFriend = await _context.Friendships
                .AnyAsync(f => ((f.SenderId == currentUserId && f.ReceiverId == u.Id) ||
                                (f.SenderId == u.Id && f.ReceiverId == currentUserId))
                               && f.Status == FriendshipStatus.Accepted);

            // B) Prüfen, ob bereits ein Gruppenmitglied
            bool isAlreadyMember = false;
            if (groupId.HasValue)
            {
                isAlreadyMember = await _context.GroupMembers
                    .AnyAsync(m => m.ChatGroupId == groupId.Value && m.UserProfileId == u.Id);
            }

            resultList.Add(new
            {
                u.Email,
                u.DisplayName,
                u.IsActive,
                IsAlreadyInGroup = isAlreadyMember,
                IsFriend = isFriend // <-- NEUES FLAG FÜR DAS WPF-XAML!
            });
        }

        return Ok(resultList);
    }


    // 5. EIGENES PROFIL LADEN
    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

        // KORREKTUR: Wir laden auch den Identity-User, um an die E-Mail zu kommen!
        var user = await _context.Set<IdentityUser>().FindAsync(currentUserId);
        var profile = await _context.UserProfiles.FindAsync(currentUserId);
        if (profile == null || user == null) return NotFound();

        // Wir fügen 'Email = user.Email' zur Antwort hinzu!
        return Ok(new { 
            profile.DisplayName, 
            ProfilePictureBase64 = profile.ProfilePictureUrl ?? string.Empty,
            Email = user.Email 
        });
    }


    // 6. PROFIL GEZIELT AKTUALISIEREN
    public record UpdateProfileDto(string DisplayName, string ProfilePictureBase64);

    [HttpPut("me")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
    {
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

        var profile = await _context.UserProfiles.FindAsync(currentUserId);
        if (profile == null) return NotFound();

        profile.DisplayName = dto.DisplayName;
        profile.ProfilePictureUrl = dto.ProfilePictureBase64;

        await _context.SaveChangesAsync();
        return Ok(new { Message = "Profil erfolgreich aktualisiert!" });
    }
}

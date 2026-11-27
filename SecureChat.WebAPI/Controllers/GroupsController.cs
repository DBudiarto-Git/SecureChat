using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureChat.Core;
using SecureChat.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using SecureChat.WebAPI.Hubs;

namespace SecureChat.WebAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class GroupsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<ChatHub> _hubContext;

    // DTOs für den Datentransfer
    public record CreateGroupDto(string GroupName, string EncryptedGroupKeyForMe, string Iv);
    public record AddMemberDto(int GroupId, string MemberEmail, string EncryptedGroupKeyForMember, string Iv);
    public record SendGroupMessageDto(int ChatGroupId, string CipherText, string Iv, string AuthTag, string MessageType = "Text", string? FileName = null);

    public GroupsController(ApplicationDbContext context, IHubContext<ChatHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    // 1. GRUPPE ERSTELLEN: api/groups/create
    [HttpPost("create")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupDto dto)
    {
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.GroupName))
            return BadRequest("Gruppenname darf nicht leer sein.");

        // Neue Gruppe anlegen
        var group = new ChatGroup
        {
            Name = dto.GroupName,
            CreatorId = currentUserId
        };

        _context.ChatGroups.Add(group);
        await _context.SaveChangesAsync(); // Generiert die Gruppen-ID

        // Den Ersteller direkt als erstes Mitglied mit seinem verschlüsselten Gruppenschlüssel eintragen
        var creatorMember = new GroupMember
        {
            ChatGroupId = group.Id,
            UserProfileId = currentUserId,
            EncryptedGroupKey = dto.EncryptedGroupKeyForMe,
            GroupKeyIv = dto.Iv
        };

        _context.GroupMembers.Add(creatorMember);
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Gruppe erfolgreich erstellt!", GroupId = group.Id });
    }

    // 2. MITGLIED EINLADEN / HINZUFÜGEN: api/groups/addmember
    [HttpPost("addmember")]
    public async Task<IActionResult> AddMemberToGroup([FromBody] AddMemberDto dto)
    {
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

        // DIAGNOSE-CHECK: Ist der Sender Mitglied?
        var isCurrentMember = await _context.GroupMembers
            .AnyAsync(m => m.ChatGroupId == dto.GroupId && m.UserProfileId == currentUserId);

        if (!isCurrentMember)
        {
            // Gibt uns im Client sofort Bescheid, was schiefgelaufen ist!
            return BadRequest($"Sicherheits-Blockade: Du bist kein Mitglied der Gruppe mit der ID '{dto.GroupId}'. Deine User-ID ist '{currentUserId}'.");
        }

        // Benutzer anhand der E-Mail suchen
        var targetUser = await _context.Set<IdentityUser>()
            .FirstOrDefaultAsync(u => u.Email == dto.MemberEmail);

        if (targetUser == null) return NotFound($"Der Benutzer mit der E-Mail '{dto.MemberEmail}' existiert nicht.");

        // Prüfen, ob der User bereits drin ist
        var alreadyMember = await _context.GroupMembers
            .AnyAsync(m => m.ChatGroupId == dto.GroupId && m.UserProfileId == targetUser.Id);

        if (alreadyMember) return BadRequest("Dieser Benutzer ist bereits Mitglied dieser Gruppe.");

        // Neuen Datensatz anlegen
        var newMember = new GroupMember
        {
            ChatGroupId = dto.GroupId,
            UserProfileId = targetUser.Id,
            EncryptedGroupKey = dto.EncryptedGroupKeyForMember,
            GroupKeyIv = dto.Iv,
            JoinedAt = DateTime.UtcNow // Stellt sicher, dass die aktuelle Uhrzeit genommen wird!
        };

        _context.GroupMembers.Add(newMember);
        await _context.SaveChangesAsync();

        // Wir prüfen, ob der eingeladene Freund gerade online ist
        if (ChatHub.OnlineUsers.TryGetValue(dto.MemberEmail, out var connectionId))
        {
            // Wir funken ein neues Event "AddedToGroup" direkt an seinen PC!
            await _hubContext.Clients.Client(connectionId).SendAsync("AddedToGroup", dto.GroupId);
        }

        return Ok(new { Message = "Mitglied erfolgreich zur Krypto-Gruppe hinzugefügt!" });
    }


    // 3. MEINE GRUPPEN ABRUFEN: api/groups/mygroups
    //[HttpGet("mygroups")]
    //public async Task<IActionResult> GetMyGroups()
    //{
    //    var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    //    if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

    //    // Alle Gruppen laden, in denen der aktuelle User Mitglied ist
    //    var groups = await _context.GroupMembers
    //        .AsNoTracking() // Deaktiviert das EF-Change-Tracking für maximale Geschwindigkeit
    //        .Where(m => m.UserProfileId == currentUserId)
    //        .Select(m => new
    //        {
    //            GroupId = m.ChatGroupId,
    //            GroupName = m.ChatGroup!.Name,
    //            EncryptedGroupKeyForMe = m.EncryptedGroupKey, // Damit der Client seinen Schlüssel holen kann
    //            Iv = m.GroupKeyIv
    //        })
    //        .ToListAsync();

    //    return Ok(groups);
    //}

    [HttpGet("mygroups")]
    public async Task<IActionResult> GetMyGroups()
    {
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

        // Alle Gruppen laden, in denen der aktuelle User Mitglied ist
        var groups = await _context.GroupMembers
            .AsNoTracking() // Deaktiviert das EF-Change-Tracking für maximale Geschwindigkeit
            .Where(m => m.UserProfileId == currentUserId)
            .Select(m => new
            {
                GroupId = m.ChatGroupId,
                GroupName = m.ChatGroup!.Name,
                EncryptedGroupKeyForMe = m.EncryptedGroupKey, // Damit der Client seinen Schlüssel holen kann
                Iv = m.GroupKeyIv,

                // 🌟 DER LIVE-GRUPPEN-ZÄHLER:
                // Zählt alle ungelesenen Nachrichten in DIESER Gruppe, die NICHT von mir selbst stammen
                UnreadCount = _context.GroupMessages.Count(msg => msg.ChatGroupId == m.ChatGroupId && msg.SenderId != currentUserId && msg.MessageStatus < 2)
            })
            .ToListAsync();

        return Ok(groups);
    }


    // 4. GRUPPEN-NACHRICHT SENDEN & SPEICHERN: api/groups/sendmessage
    [HttpPost("sendmessage")]
    public async Task<IActionResult> SendGroupMessage([FromBody] SendGroupMessageDto dto)
    {
        // 1. Benutzer-ID aus dem Token holen
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

        // 🌟 DIE RADIKALE RETTUNG: Wir werfen .Include() komplett raus!
        // Wir prüfen die Mitgliedschaft rein über die flachen IDs (ChatGroupId und UserProfileId).
        // Das ist zu 100% immun gegen unvollständige Navigationseigenschaften oder EF-Core-Mapping-Fehler!
        var isMember = await _context.GroupMembers
            .AnyAsync(m => m.ChatGroupId == dto.ChatGroupId && m.UserProfileId == currentUserId);

        if (!isMember) return Forbid("Du bist kein Mitglied dieser Gruppe.");

        // Echte E-Mail des Senders krisenfest laden
        var identityUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == currentUserId); // b01efc3a-3089-42c5-bf5d-dc99d303ab14
        string senderEmail = identityUser?.Email ?? "Unbekannt";

        // 2. Nachricht permanent in der SQL-Datenbank sichern
        var message = new GroupMessage
        {
            ChatGroupId = dto.ChatGroupId, // Mappt den DTO-Wert direkt auf die echte SQL-Spalte! [TS_2]
            SenderId = currentUserId,
            CipherText = dto.CipherText,
            Iv = dto.Iv,
            AuthTag = dto.AuthTag,
            Timestamp = DateTime.UtcNow,
            MessageType = dto.MessageType ?? "Text",
            FileName = dto.FileName,
            MessageStatus = 0          // Reicht die '0' an die NOT-NULL-Spalte weiter [TS_4]
        };

        _context.GroupMessages.Add(message);
        await _context.SaveChangesAsync(); // 🚀 SQL SERVER AKZEPTIERT DIE ZEILE JETZT ZU 100%! [TS_6]

        try
        {
            // 3. E-Mails der anderen Mitglieder zweistufig und stabil holen
            var memberIds = await _context.GroupMembers
                .Where(m => m.ChatGroupId == dto.ChatGroupId && m.UserProfileId != currentUserId)
                .Select(m => m.UserProfileId)
                .ToListAsync();

            var otherMembers = await _context.Users
                .Where(u => memberIds.Contains(u.Id) && !string.IsNullOrEmpty(u.Email))
                .Select(u => u.Email)
                .ToListAsync();

            // 4. ECHTZEIT-VERTEILUNG VIA SIGNALR
            foreach (var email in otherMembers)
            {
                if (!string.IsNullOrEmpty(email) && ChatHub.OnlineUsers.TryGetValue(email, out var connectionId))
                {
                    await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveGroupMessage",
                        dto.ChatGroupId, senderEmail, dto.CipherText, dto.Iv, dto.AuthTag,
                        dto.MessageType, dto.FileName);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SignalR-Gruppen-Warnung] {ex.Message}");
        }

        // 5. Saubere ID-Rückgabe für den Client [INDEX]
        return Ok(new { id = message.Id, Message = "Gruppennachricht sicher gespeichert und übertragen." });
    }


    // 5. GRUPPEN-CHAT-VERLAUF LADEN: api/groups/history?groupId=5
    [HttpGet("history")]
    public async Task<IActionResult> GetGroupHistory([FromQuery] int groupId)
    {
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

        // Security-Check: Ist der anfragende User Mitglied? (bestehend)
        var isMember = await _context.GroupMembers.AnyAsync(m => m.ChatGroupId == groupId && m.UserProfileId == currentUserId);
        if (!isMember) return Forbid("Keine Berechtigung für diesen Gruppenverlauf.");

        // Alle verschlüsselten Nachrichten dieser Gruppe chronologisch laden
        var messages = await _context.GroupMessages
            .AsNoTracking() // ⚡ DEAKTIVIERT DAS SCHWERE TRACKING FÜR MAXIMALE RECHENPOWER!
            .Where(m => m.ChatGroupId == groupId)
            .OrderBy(m => m.Timestamp)
            .Join(_context.Users,
                msg => msg.SenderId,
                user => user.Id,
                (msg, user) => new
                {
                    Id = msg.Id,
                    MessageStatus = msg.MessageStatus,
                    GroupId = msg.ChatGroupId,
                    SenderEmail = user.Email,

                    // 🔑 DER PERFORMANCE-BOOST: 
                    // Reine Textnachrichten laden wir sofort. Bei Medien (Image, Audio, Video, File)
                    // blenden wir die schweren Dateibytes im Haupt-Stream aus!
                    CipherText = (msg.MessageType == "Text") ? msg.CipherText : string.Empty,

                    msg.Iv,
                    msg.AuthTag,
                    msg.Timestamp,
                    msg.MessageType,
                    msg.FileName
                })
            .ToListAsync();

        return Ok(messages);
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkGroupMessagesAsRead([FromQuery] int groupId)
    {
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

        // Security-Check: Ist der Benutzer überhaupt in der Gruppe?
        var isMember = await _context.GroupMembers
            .AnyAsync(m => m.ChatGroupId == groupId && m.UserProfileId == currentUserId);

        if (!isMember) return Forbid("Kein Zugriff auf diese Gruppe.");

        // Wir holen alle ungelesenen Nachrichten dieser Gruppe, die NICHT von mir selbst stammen
        var unreadGroupMessages = await _context.GroupMessages
            .Where(m => m.ChatGroupId == groupId && m.SenderId != currentUserId && m.MessageStatus < 2)
            .ToListAsync();

        foreach (var msg in unreadGroupMessages)
        {
            msg.MessageStatus = 2; // Auf "Gelesen" (Blau) setzen [INDEX]
        }

        await _context.SaveChangesAsync(); // Permanent in der SQL Server-Datenbank brennen [INDEX]

        // ===================================================================
        // 🌟 DIE RETTUNG: Live-Verteilung über SignalR aktivieren!
        // ===================================================================
        try
        {
            // 1. Wir holen uns die UserProfileIds aller Mitglieder dieser Gruppe
            var memberIds = await _context.GroupMembers
                .Where(m => m.ChatGroupId == groupId)
                .Select(m => m.UserProfileId)
                .ToListAsync();

            // 2. Wir laden die dazugehörigen E-Mails aus der Identity-Users-Tabelle
            var members = await _context.Users
                .Where(u => memberIds.Contains(u.Id) && !string.IsNullOrEmpty(u.Email))
                .Select(u => u.Email)
                .ToListAsync();

            // 3. Wir informieren jedes Online-Mitglied im selben Wimpernschlag
            foreach (var email in members)
            {
                // Ersetze 'ChatHub' durch den exakten Namen deiner SignalR-Hub-Klasse, falls abweichend
                if (!string.IsNullOrEmpty(email) && ChatHub.OnlineUsers.TryGetValue(email, out var connectionId))
                {
                    // Wir feuern das Event 'GroupChatReadByPartner' direkt an den Client
                    // Achte darauf, dass '_hubContext' oben in deinem Controller via Dependency Injection geladen ist!
                    await _hubContext.Clients.Client(connectionId).SendAsync("GroupChatReadByPartner", groupId);
                }
            }
        }
        catch (Exception sigEx)
        {
            // Verhindert den HTTP-Absturz, falls im SignalR-Netzwerk ein Hänger war
            System.Diagnostics.Debug.WriteLine($"[SignalR-Gruppenlese-Fehler] {sigEx.Message}");
        }

        // 🌟 ECHTZEIT-BENACHRICHTIGUNG: Wir informieren die Gruppe via SignalR, 
        // dass die Nachrichten soeben aktiv gelesen wurden!
        // (Hier kannst du euren Gruppen-Hub-Context nutzen, um ein Event wie "GroupChatRead" zu feuern)

        return Ok(new { Message = "Gruppennachrichten erfolgreich als gelesen markiert." });
    }

    // 🌟 NEU: Der dedizierte Medien-Streamer für das Gruppen-Lazy-Loading!
    [HttpGet("download/{messageId}")]
    public async Task<IActionResult> DownloadGroupMessageFile(int messageId)
    {
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

        // Nachricht aus der SQL Server-Datenbank laden
        var msg = await _context.GroupMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == messageId);
        if (msg == null) return NotFound("Nachricht nicht gefunden.");

        // Security-Check: Ist der Anfragende überhaupt Mitglied dieser Gruppe?
        var isMember = await _context.GroupMembers.AnyAsync(m => m.ChatGroupId == msg.ChatGroupId && m.UserProfileId == currentUserId);
        if (!isMember) return Forbid("Keine Berechtigung für diese Datei.");

        // Liefert den reinen verschlüsselten Base64-String (inkl. der kombinierten Schlüssel-Parts) zurück
        return Content(msg.CipherText ?? string.Empty, "text/plain", System.Text.Encoding.UTF8);
    }

}


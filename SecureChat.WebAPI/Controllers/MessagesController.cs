using Microsoft.AspNetCore.Authorization;
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
using Microsoft.AspNetCore.Identity;

namespace SecureChat.WebAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<ChatHub> _hubContext;
    public record SendMessageDto(string ReceiverEmail, string EncryptedKey, string CipherText, string Iv, string AuthTag, string MessageType = "Text", string? FileName = null);

    public MessagesController(ApplicationDbContext context, IHubContext<ChatHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageDto dto)
    {
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

        var senderUser = await _context.Set<IdentityUser>().FirstOrDefaultAsync(u => u.Id == currentUserId);
        var receiverUser = await _context.Set<IdentityUser>().FirstOrDefaultAsync(u => u.Email == dto.ReceiverEmail);

        if (senderUser == null || receiverUser == null)
            return NotFound("Sender oder Empfänger nicht gefunden.");

        string senderEmail = senderUser.Email ?? string.Empty;

        var message = new ChatMessage
        {
            SenderId = senderUser.Id,
            ReceiverId = receiverUser.Id,
            EncryptedAesKey = dto.EncryptedKey,
            CipherText = dto.CipherText,
            Iv = dto.Iv,
            AuthTag = dto.AuthTag,
            Timestamp = DateTime.UtcNow,
            MessageType = dto.MessageType,
            FileName = dto.FileName
        };

        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync(); // HIER generiert die SQL-Datenbank die echte ID!

        if (ChatHub.OnlineUsers.TryGetValue(dto.ReceiverEmail, out var connectionId))
        {
            // 🌟 KORREKTUR 1: Wir übergeben die echte message.Id als NEUEN Parameter direkt an SignalR!
            // Der Empfänger-Client benötigt diese ID zwingend für den Rückruf!
            await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveMessage",
                senderEmail, dto.ReceiverEmail, dto.EncryptedKey, dto.CipherText, dto.Iv, dto.AuthTag,
                dto.MessageType, dto.FileName, message.Id); // <-- 🌟 message.Id HIER ALS LETZTEN PARAMETER ERGÄNZT!
        }

        // 🌟 KORREKTUR 2: Wir ändern den Key im JSON auf "id", damit der Client ihn fehlerfrei parsen kann!
        return Ok(new { Message = "Nachricht sicher gespeichert.", id = message.Id });
    }

    // 1. CHAT-VERLAUF LADEN (Filtert ab jetzt gelöschte Nachrichten heraus!)
    //[HttpGet("history")]
    //public async Task<IActionResult> GetChatHistory([FromQuery] string withUser)
    //{
    //    var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    //    if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

    //    var currentUser = await _context.Set<IdentityUser>().FirstOrDefaultAsync(u => u.Id == currentUserId);
    //    var targetUser = await _context.Set<IdentityUser>().FirstOrDefaultAsync(u => u.Email == withUser);

    //    if (currentUser == null || targetUser == null) return NotFound();

    //    // KORREKTUR: Wir laden nur Nachrichten, die der aktuelle User NICHT gelöscht hat!
    //    var messages = await _context.ChatMessages
    //        .Where(m => (m.SenderId == currentUser.Id && m.ReceiverId == targetUser.Id && !m.DeletedBySender) ||
    //                   (m.SenderId == targetUser.Id && m.ReceiverId == currentUser.Id && !m.DeletedByReceiver))
    //        .OrderBy(m => m.Timestamp)
    //        .Select(m => new
    //        {
    //            Id = m.Id, // 🌟 UNBEDINGT ERFORDERLICH: Die eindeutige Nachrichten-ID für den Client!
    //            SenderEmail = m.SenderId == currentUser.Id ? currentUser.Email : withUser,
    //            ReceiverEmail = m.ReceiverId == currentUser.Id ? currentUser.Email : withUser,
    //            m.EncryptedAesKey,
    //            m.CipherText,
    //            m.Iv,
    //            m.AuthTag,
    //            m.Timestamp,
    //            m.MessageType, // <-- NEU!
    //            m.FileName,     // <-- NEU!
    //            MessageStatus = m.MessageStatus // 🌟 EBENSO WICHTIG: Damit alte Häkchen beim Laden blau/grau bleiben!
    //        })
    //        .ToListAsync();

    //    return Ok(messages);
    //}

    [HttpGet("history")]
    public async Task<IActionResult> GetChatHistory([FromQuery] string withUser)
    {
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

        var me = await _context.Set<IdentityUser>().FirstOrDefaultAsync(u => u.Id == currentUserId);
        var partner = await _context.Set<IdentityUser>().FirstOrDefaultAsync(u => u.Email == withUser);

        if (me == null || partner == null) return NotFound();

        var myEmail = me.Email ?? string.Empty;

        // 🌟 DIE ARCHITEKTUR-RETTUNG: Wir projizieren die Daten schlank!
        //var history = await _context.ChatMessages
        //    .Where(m => (m.SenderId == me.Id && m.ReceiverId == partner.Id) || (m.SenderId == partner.Id && m.ReceiverId == me.Id))
        //    .OrderBy(m => m.Timestamp)
        //    .Select(m => new
        //    {
        //        Id = m.Id,
        //        SenderEmail = m.SenderId == me.Id ? myEmail : withUser,
        //        ReceiverEmail = m.ReceiverId == me.Id ? myEmail : withUser,
        //        Timestamp = m.Timestamp,
        //        MessageType = m.MessageType,
        //        FileName = m.FileName,
        //        MessageStatus = m.MessageStatus,
        //        Iv = m.Iv,
        //        AuthTag = m.AuthTag,
        //        EncryptedAesKey = m.EncryptedAesKey,

        //        // 🔑 DER LAZY-TRICK: Reine Texte laden wir sofort. 
        //        // Bei schweren Medien (Bildern, Videos, Audio) schicken wir den CipherText NICHT mit!
        //        // Das macht die SQL-Abfrage und den HTTP-Call blitzschnell!
        //        CipherText = (m.MessageType == "Text") ? m.CipherText : string.Empty
        //    })
        //    .ToListAsync();

        // 🌟 DIE RETTUNG: Wir laden Medien-Inhalte NICHT im Haupt-Stream mit!
        var history = await _context.ChatMessages
            .AsNoTracking()
            .Where(m => (m.SenderId == me.Id && m.ReceiverId == partner.Id) || (m.SenderId == partner.Id && m.ReceiverId == me.Id))
            .OrderBy(m => m.Timestamp)
            .Select(m => new 
            {
                Id = m.Id,
                SenderEmail = m.SenderId == me.Id ? myEmail : withUser,
                ReceiverEmail = m.ReceiverId == me.Id ? myEmail : withUser,
                Timestamp = m.Timestamp,
                MessageType = m.MessageType,
                FileName = m.FileName,
                MessageStatus = m.MessageStatus,
                Iv = m.Iv,
                AuthTag = m.AuthTag,
                EncryptedAesKey = m.EncryptedAesKey,
                // 🔑 Textnachrichten werden sofort geliefert, Medien bleiben leer und leicht!
                CipherText = (m.MessageType == "Text") ? m.CipherText : string.Empty
            })
            .ToListAsync();

        return Ok(history);
    }
    //[HttpGet("history")]
    //public async Task<IActionResult> GetChatHistory([FromQuery] string withUser)
    //{
    //    var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    //    if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

    //    var me = await _context.Set<IdentityUser>().FirstOrDefaultAsync(u => u.Id == currentUserId);
    //    var partner = await _context.Set<IdentityUser>().FirstOrDefaultAsync(u => u.Email == withUser);

    //    if (me == null || partner == null) return NotFound();

    //    var myEmail = me.Email ?? string.Empty;

    //    // 🌟 DIE LAUFZEIT-RETTUNG: AsNoTracking() bricht die EF-Core-Trägheit!
    //    // Dadurch holt der SQL Server die Daten ohne RAM-Zwischenstopp direkt aus der Tabelle.
    //    var history = await _context.ChatMessages
    //        .AsNoTracking() // ⚡ DEAKTIVIERT DAS SCHWERE TRACKING!
    //        .Where(m => (m.SenderId == me.Id && m.ReceiverId == partner.Id) || (m.SenderId == partner.Id && m.ReceiverId == me.Id))
    //        .OrderBy(m => m.Timestamp)
    //        .ToListAsync();

    //    return Ok(history);
    //}

    // 🌟 NEU: Der dedizierte Medien-Streamer für dein Lazy-Loading!
    [HttpGet("download/{id}")]
    public async Task<IActionResult> DownloadMessageFile(int id)
    {
        // Holt sich das schwere Paket rein isoliert über die ID aus dem SQL Server
        var msg = await _context.ChatMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
        if (msg == null) return NotFound();

        // Liefert den reinen verschlüsselten Base64-String als flachen Text zurück
        return Content(msg.CipherText ?? string.Empty, "text/plain", System.Text.Encoding.UTF8);
    }


    // 2. NEU - STRATEGIE A: VERLAUF FÜR MICH LÖSCHEN
    [HttpDelete("clear")]
    public async Task<IActionResult> ClearChatHistory([FromQuery] string withUser)
    {
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

        var targetUser = await _context.Set<IdentityUser>().FirstOrDefaultAsync(u => u.Email == withUser);
        if (targetUser == null) return NotFound("Gegensprecher nicht gefunden.");

        // Wir holen alle Nachrichten zwischen den beiden Benutzern
        var messages = await _context.ChatMessages
            .Where(m => (m.SenderId == currentUserId && m.ReceiverId == targetUser.Id) ||
                       (m.SenderId == targetUser.Id && m.ReceiverId == currentUserId))
            .ToListAsync();

        foreach (var m in messages)
        {
            if (m.SenderId == currentUserId) m.DeletedBySender = true;     // Wenn ich der Absender war
            if (m.ReceiverId == currentUserId) m.DeletedByReceiver = true; // Wenn ich der Empfänger war
        }

        await _context.SaveChangesAsync();
        return Ok(new { Message = "Chatverlauf wurde für dich erfolgreich geleert." });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead([FromQuery] string withUser)
    {
        // 🌟 DIE RETTUNG: Wir extrahieren die E-Mail über ein krisenfestes 3-Wege-Netz!
        // Pfad 1: Der Microsoft-Standard-Name-Claim
        string? myEmail = User.Identity?.Name;

        // Pfad 2: Falls null, suchen wir nach dem klassischen Email-Claim Typen
        if (string.IsNullOrEmpty(myEmail))
        {
            myEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        }

        // Pfad 3: Falls immer noch null, suchen wir nach dem flachen JWT-Namen "email"
        if (string.IsNullOrEmpty(myEmail))
        {
            myEmail = User.FindFirst("email")?.Value;
        }

        // Sicherheits-Härtung: Wenn absolut gar nichts im Token steht oder der Parameter fehlt
        if (string.IsNullOrEmpty(myEmail) || string.IsNullOrEmpty(withUser))
        {
            // Gibt dir im Debugger Aufschluss, was gefehlt hat
            System.Diagnostics.Debug.WriteLine($"[Krypto-Fehler] myEmail: '{myEmail}', withUser: '{withUser}'");
            return Unauthorized("Authentifizierung fehlgeschlagen: E-Mail-Claim im Token nicht auffindbar.");
        }

        // ===================================================================
        // DEIN BESTEHENDER CODE (IDs holen, Status auf 2 setzen & SaveChanges)
        // ===================================================================
        var me = await _context.Users.FirstOrDefaultAsync(u => u.Email == myEmail);
        var partner = await _context.Users.FirstOrDefaultAsync(u => u.Email == withUser);

        if (me == null || partner == null)
            return NotFound("Benutzer konnten im System nicht ermittelt werden.");

        var unreadMessages = await _context.ChatMessages
            .Where(m => m.SenderId == partner.Id && m.ReceiverId == me.Id && m.MessageStatus < 2)
            .ToListAsync();

        foreach (var msg in unreadMessages)
        {
            msg.MessageStatus = 2; // Setzt den Status auf "Gelesen" (Zwei blaue Häkchen)
        }

        // Brennt die blauen Häkchen unumstößlich und permanent in die SQL-Tabelle!
        await _context.SaveChangesAsync();

        return Ok(new { Message = $"Alle Nachrichten von {withUser} wurden erfolgreich als gelesen markiert." });
    }


    [HttpPost("read-all-group")]
    public async Task<IActionResult> MarkGroupMessagesAsRead([FromQuery] int groupId)
    {
        // 1. Benutzer über das Token identifizieren
        var myEmail = User.Identity?.Name;
        if (string.IsNullOrEmpty(myEmail)) return Unauthorized();

        var me = await _context.Users.FirstOrDefaultAsync(u => u.Email == myEmail);
        if (me == null) return NotFound("Benutzer nicht gefunden.");

        // 2. Alle ungelesenen Nachrichten dieser Gruppe, die NICHT von mir selbst stammen,
        // für mich als gelesen (Status 2) markieren
        // (Hinweis: Passe 'GroupMessages' und 'SenderId' ggf. an deine exakten DB-Tabellennamen an)
        var unreadGroupMessages = await _context.GroupMessages
            .Where(m => m.ChatGroupId == groupId && m.SenderId != me.Id && m.MessageStatus < 2)
            .ToListAsync();

        foreach (var msg in unreadGroupMessages)
        {
            msg.MessageStatus = 2;
        }

        // 3. In die SQL-Datenbank wegschreiben
        await _context.SaveChangesAsync();
        return Ok(new { Message = $"Alle Gruppen-Nachrichten für Gruppe {groupId} wurden als gelesen markiert." });
    }
}

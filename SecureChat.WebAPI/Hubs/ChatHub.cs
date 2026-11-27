using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SecureChat.Infrastructure;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace SecureChat.WebAPI.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly ApplicationDbContext _context;

    // Thread-sicheres Verzeichnis aller Online-User: [Key: Email -> Value: ConnectionId]
    public static readonly ConcurrentDictionary<string, string> OnlineUsers = new();

    public ChatHub(ApplicationDbContext context)
    {
        _context = context;
    }

    // 1. EVENT: USER GEHT ONLINE
    //public override async Task OnConnectedAsync()
    //{
    //    var currentUserId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    //    if (!string.IsNullOrEmpty(currentUserId))
    //    {
    //        // Verbindung im RAM registrieren
    //        OnlineUsers[currentUserId] = Context.ConnectionId;

    //        // Echte E-Mail aus der Identity-Tabelle holen
    //        var user = await _context.Set<IdentityUser>().FindAsync(currentUserId);
    //        if (user != null && !string.IsNullOrEmpty(user.Email))
    //        {
    //            // Wir registrieren den User zusätzlich unter seiner E-Mail für die Abfragen
    //            OnlineUsers[user.Email] = Context.ConnectionId;

    //            // WICHTIG: Wir funken die echte E-MAIL an alle anderen Clients heraus!
    //            await Clients.Others.SendAsync("UserStatusChanged", user.Email, true);
    //        }
    //    }

    //    await base.OnConnectedAsync();
    //}
    public override async Task OnConnectedAsync()
    {
        var currentUserId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(currentUserId))
        {
            // 1. In der globalen Online-Liste unter der ID registrieren
            OnlineUsers[currentUserId] = Context.ConnectionId;

            // Echte E-Mail aus der Identity-Tabelle holen
            var user = await _context.Set<IdentityUser>().FindAsync(currentUserId);
            if (user != null && !string.IsNullOrEmpty(user.Email))
            {
                // 2. In der globalen Online-Liste unter der E-Mail registrieren
                OnlineUsers[user.Email] = Context.ConnectionId;

                // 🌟 KORREKTUR: Wir informieren ALLE ANDEREN über das alte, funktionierende Event!
                // Das lässt den Kreis bei den bereits eingeloggten Usern sofort live GRÜN werden.
                await Clients.Others.SendAsync("UserStatusChanged", user.Email, true);
            }
        }

        await base.OnConnectedAsync();
    }

    // 🌟 DER SICHERSYNC-ANSTOSS VOM CLIENT
    public async Task RequestActiveOnlineUsers()
    {
        var currentUserId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(currentUserId))
        {
            var user = await _context.Set<IdentityUser>().FindAsync(currentUserId);
            if (user != null && !string.IsNullOrEmpty(user.Email))
            {
                // Wir filtern alle Keys heraus, die echte E-Mails sind
                var currentlyOnlineEmails = OnlineUsers.Keys
                    .Where(k => k.Contains("@"))
                    .ToList();

                // Wir senden dem neu eingewählten User die Liste aller, die bereits da sind!
                await Clients.Caller.SendAsync("UpdateOnlineUsersList", currentlyOnlineEmails);

                // Sicheitsnetz: Falls das Event beim Verbinden zu früh war, feuern wir es hier noch einmal ab
                await Clients.Others.SendAsync("UserStatusChanged", user.Email, true);
            }
        }
    }


    // 2. EVENT: USER GEHT OFFLINE
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var currentUserId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(currentUserId))
        {
            OnlineUsers.TryRemove(currentUserId, out _);

            var user = await _context.Set<IdentityUser>().FindAsync(currentUserId);
            if (user != null && !string.IsNullOrEmpty(user.Email))
            {
                OnlineUsers.TryRemove(user.Email, out _);

                // WICHTIG: Wir funken das OFFLINE-Event mit der echten E-MAIL heraus!
                await Clients.Others.SendAsync("UserStatusChanged", user.Email, false);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }


    // 3. API-FUNKTION: PRÜFEN, OB EIN BESTIMMTER USER ONLINE IST
    public async Task<bool> IsUserOnline(string email)
    {
        // 1. Direkt im Wörterbuch nach der E-Mail suchen
        if (OnlineUsers.ContainsKey(email)) return true;

        // 2. Sicherheits-Check: Falls in der Liste GUIDs stecken, übersetzen wir die E-Mail kurz
        var user = await _context.Set<IdentityUser>()
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user != null && OnlineUsers.ContainsKey(user.Id)) return true;

        return false;

    }

    // 4. DER GEZIELTE POSTBOTE: Sendet die E2EE-Nachricht NUR an den exakten Empfänger
    public async Task SendEncryptedMessage(string receiverEmail, string encryptedKey, string cipherText, string iv, string authTag)
    {
        var senderEmail = Context.User?.Identity?.Name ?? "Unbekannt";

        // Prüfen, ob der Empfänger gerade online ist
        if (OnlineUsers.TryGetValue(receiverEmail, out var connectionId))
        {
            // Direkt und exakt nur an die ConnectionId des Empfängers senden!
            await Clients.Client(connectionId).SendAsync("ReceiveMessage", senderEmail, receiverEmail, encryptedKey, cipherText, iv, authTag);
        }

        // Die Nachricht auch an die anderen Geräte des Senders spiegeln (für die eigene Chat-Historie)
        await Clients.Caller.SendAsync("ReceiveMessage", senderEmail, receiverEmail, encryptedKey, cipherText, iv, authTag);
    }

    // 1. Signalisiert dem Sender, dass die Nachricht auf dem Gerät angekommen ist
    public async Task ConfirmDelivery(string senderEmail, int messageId)
    {
        if (OnlineUsers.TryGetValue(senderEmail, out var connectionId))
        {
            await Clients.Client(connectionId).SendAsync("MessageDelivered", messageId);
        }
    }

    // 2. Signalisiert dem Partner, dass der Chat geöffnet und die Nachrichten gelesen wurden
    public async Task ConfirmRead(string partnerEmail)
    {
        var myEmail = Context.User?.Identity?.Name;
        if (string.IsNullOrEmpty(myEmail)) return;

        if (OnlineUsers.TryGetValue(partnerEmail, out var connectionId))
        {
            await Clients.Client(connectionId).SendAsync("ChatReadByPartner", myEmail);
        }
    }

    // In ChatHub.cs innerhalb deines Hubs hinzufügen:
    public async Task RequestOnlineUsersList()
    {
        var userEmail = Context.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(userEmail))
        {
            // 1. Sicherstellen, dass wir in der Online-Liste registriert sind
            OnlineUsers[userEmail] = Context.ConnectionId;

            // 🌟 DIE RETTUNG: Wir schicken dem Anrufer JETZT die Liste,
            // da sein Client nun garantiert zu 100% empfangsbereit ist!
            var currentlyOnlineList = OnlineUsers.Keys.ToList();
            await Clients.Caller.SendAsync("UpdateOnlineUsersList", currentlyOnlineList);

            // 🌟 ALLE ANDEREN informieren, dass wir da sind (lässt deren Kreise grün werden)
            await Clients.Others.SendAsync("UserWentOnline", userEmail);
        }
    }

    // 📞 VOICE-CALL PIPELINE: Signalweiterleitung für Sprachanrufe

    //public async Task SendCallOffer(string targetEmail, string sdpOffer)
    //{
    //    // Leitet das Anruf-Angebot (SDP Offer) an den Empfänger weiter
    //    if (OnlineUsers.TryGetValue(targetEmail, out var connectionId))
    //    {
    //        var myEmail = Context.User?.Identity?.Name ?? "Unbekannt";
    //        await Clients.Client(connectionId).SendAsync("ReceiveCallOffer", myEmail, sdpOffer);
    //    }
    //}

    // 📞 VOICE-CALL PIPELINE: Signalweiterleitung für Sprachanrufe im ChatHub
    public async Task SendCallOffer(string targetEmail, string sdpOffer)
    {
        if (OnlineUsers.TryGetValue(targetEmail, out var connectionId))
        {
            // 🌟 DIE RETTUNG: Krisenfeste Drei-Wege-Claim-Extraktion für den Hub-Kontext
            string? myEmail = Context.User?.Identity?.Name;

            if (string.IsNullOrEmpty(myEmail))
            {
                myEmail = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            }
            if (string.IsNullOrEmpty(myEmail))
            {
                myEmail = Context.User?.FindFirst("email")?.Value;
            }

            // Falls alles fehlschlägt, nutzen wir zur Sicherheit ein sauberes Fallback-Log
            if (string.IsNullOrEmpty(myEmail))
            {
                myEmail = "unbekannter_anrufer@securechat.de";
            }

            System.Diagnostics.Debug.WriteLine($"[Hub-Voice] Leite Anruf-Angebot weiter von {myEmail} an Connection {connectionId}");

            // Wir senden die ECHTE, verifizierte E-Mail an den Empfänger mit!
            await Clients.Client(connectionId).SendAsync("ReceiveCallOffer", myEmail, sdpOffer);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[Hub-Voice-Warnung] Ziel {targetEmail} ist aktuell offline im Hub.");
        }
    }

    public async Task SendCallAnswer(string targetEmail, string sdpAnswer)
    {
        // Leitet die Anruf-Annahme (SDP Answer) zurück an den Anrufer
        if (OnlineUsers.TryGetValue(targetEmail, out var connectionId))
        {
            await Clients.Client(connectionId).SendAsync("ReceiveCallAnswer", sdpAnswer);
        }
    }

    public async Task SendIceCandidate(string targetEmail, string candidateJson)
    {
        // Tauscht Netzwerk-Kandidaten (IP-Adressen) im Hintergrund aus
        if (OnlineUsers.TryGetValue(targetEmail, out var connectionId))
        {
            await Clients.Client(connectionId).SendAsync("ReceiveIceCandidate", candidateJson);
        }
    }

    // 📞 VOICE-CALL PIPELINE: Signalweiterleitung für das Beenden eines Anrufs
    public async Task SendEndCall(string targetEmail)
    {
        if (OnlineUsers.TryGetValue(targetEmail, out var connectionId))
        {
            var myEmail = Context.User?.Identity?.Name;
            if (string.IsNullOrEmpty(myEmail))
            {
                myEmail = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? Context.User?.FindFirst("email")?.Value ?? "Unbekannt";
            }

            System.Diagnostics.Debug.WriteLine($"[Hub-Voice] Weiterleitung CallEnded von {myEmail} an Connection {connectionId}");
            await Clients.Client(connectionId).SendAsync("ReceiveCallEnded", myEmail);
        }
    }

}

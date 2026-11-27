using System;

namespace SecureChat.Core;

public enum FriendshipStatus
{
    Pending,    // Anfrage gesendet, wartet auf Bestätigung
    Accepted,   // Anfrage angenommen, Krypto-Kanal offen
    Rejected    // Anfrage abgelehnt
}

public class Friendship
{
    public int Id { get; set; }

    // Der Benutzer, der die Anfrage sendet (Sender)
    public string SenderId { get; set; } = string.Empty;
    public UserProfile? Sender { get; set; }

    // Der Benutzer, der die Anfrage erhält (Empfänger)
    public string ReceiverId { get; set; } = string.Empty;
    public UserProfile? Receiver { get; set; }

    // Aktueller Zustand der Beziehung
    public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ActionAt { get; set; } // Wann wurde angenommen/abgelehnt
}

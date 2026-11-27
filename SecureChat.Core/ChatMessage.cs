using System;

namespace SecureChat.Core;

public class ChatMessage
{
    public int Id { get; set; }

    // Wer hat die Nachricht gesendet (GUID des Identity-Users)
    public string SenderId { get; set; } = string.Empty;
    public UserProfile? Sender { get; set; }

    // Wer soll die Nachricht empfangen (GUID des Identity-Users)
    public string ReceiverId { get; set; } = string.Empty;
    public UserProfile? Receiver { get; set; }

    // KRYPTO-PAYLOADS (Das E2EE-Paket)
    public string EncryptedAesKey { get; set; } = string.Empty; // Mit RSA verschlüsselter AES-Key
    public string CipherText { get; set; } = string.Empty;      // Mit AES-GCM verschlüsselte Nachricht
    public string Iv { get; set; } = string.Empty;              // Initialisierungsvektor / Nonce
    public string AuthTag { get; set; } = string.Empty;         // AES-GCM Authentifizierungs-Tag

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; } = false;
    public bool DeletedBySender { get; set; } = false;
    public bool DeletedByReceiver { get; set; } = false;
    public string MessageType { get; set; } = "Text"; // "Text", "Image" oder "File"
    public string? FileName { get; set; } 
    // Optionaler Name bei Dateien
    // In ChatMessage.cs innerhalb der Klasse ChatMessage hinzufügen:
    // 0 = Gesendet (✓), 1 = Zugestellt (✓✓ grau), 2 = Gelesen (✓✓ blau)
    public int MessageStatus { get; set; } = 0;

}

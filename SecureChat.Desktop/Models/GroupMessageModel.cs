using System;

namespace SecureChat.Desktop.Models;

public class GroupMessageModel
{
    public int GroupId { get; set; }
    public string SenderEmail { get; set; } = string.Empty;
    public string CipherText { get; set; } = string.Empty;
    public string Iv { get; set; } = string.Empty;
    public string AuthTag { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }

    // NEU: Damit der Client den Typ und Namen vom Server fehlerfrei einlesen kann
    public string MessageType { get; set; } = "Text";
    public string? FileName { get; set; }

    // 🌟 DIE RETTUNG FÜR DEN COMPILER:
    // Wir fügen die ID und den MessageStatus hinzu, damit die Lade-Schleife sie auslesen kann!
    public int Id { get; set; }

    // 0 = Gesendet (✓), 1 = Zugestellt (✓✓ grau), 2 = Gelesen (✓✓ blau)
    public int MessageStatus { get; set; } = 0;
}

using System;

namespace SecureChat.Core;

public class GroupMessage
{
    public int Id { get; set; }

    // 🌟 KORREKTUR: Das ist der einzig wahre Fremdschlüssel zu deiner SQL-Tabelle!
    public int ChatGroupId { get; set; }
    public ChatGroup? ChatGroup { get; set; }

    public string SenderId { get; set; } = string.Empty;
    public UserProfile? Sender { get; set; }

    public string CipherText { get; set; } = string.Empty;
    public string Iv { get; set; } = string.Empty;
    public string AuthTag { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string MessageType { get; set; } = "Text";
    public string? FileName { get; set; }

    // ❌ DIE URSACHE GELÖSCHT: Keine 'public int GroupId { get; set; }' Zeile mehr hier drin!

    public int MessageStatus { get; set; } = 0; // Bedient deine NOT-NULL Spalte [TS_4]
}


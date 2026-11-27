using System;

namespace SecureChat.Core;

public class GroupMember
{
    public int Id { get; set; }

    public int ChatGroupId { get; set; }
    public ChatGroup? ChatGroup { get; set; }

    // Die Identity-GUID des Mitglieds
    public string UserProfileId { get; set; } = string.Empty;
    public UserProfile? UserProfile { get; set; }

    // === KRYPTO-FUNDAMENT FÜR E2EE-GRUPPEN ===
    // Der symmetrische Gruppen-AES-Schlüssel, verschlüsselt mit dem RSA-Public-Key DIESES Mitglieds!
    public string EncryptedGroupKey { get; set; } = string.Empty;
    public string GroupKeyIv { get; set; } = string.Empty;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

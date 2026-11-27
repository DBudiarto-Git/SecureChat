using System;

namespace SecureChat.Core;

public class UserProfile
{
    public string Id { get; set; } = string.Empty; // Verknüpfung zur Identity-User-ID
    public string DisplayName { get; set; } = string.Empty;
    public string ProfilePictureUrl { get; set; } = string.Empty; // Hier wird das Base64-Bild gespeichert!
    public string PublicKeyXml { get; set; } = string.Empty; // Der RSA Public Key für die E2EE!
    public string RsaPublicKeyXml { get; set; } = string.Empty; // Der öffentliche RSA-Schlüssel des Nutzers
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}


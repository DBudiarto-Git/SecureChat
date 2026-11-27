namespace SecureChat.Desktop.Models;

public record EncryptedMessagePackage(
    string Sender,
    string Receiver,
    string EncryptedAesKey, // Mit RSA verschlüsselt
    string CipherText,      // Mit AES-GCM verschlüsselt
    string Iv,              // AES-GCM Nonce
    string AuthTag          // AES-GCM Authentifizierungs-Tag
);


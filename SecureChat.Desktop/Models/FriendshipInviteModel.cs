namespace SecureChat.Desktop.Models;

public class FriendshipInviteModel
{
    public int FriendshipId { get; set; }
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderDisplayName { get; set; } = string.Empty;
}


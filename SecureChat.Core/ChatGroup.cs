using System;
using System.Collections.Generic;

namespace SecureChat.Core;

public class ChatGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Wer hat die Gruppe erstellt (GUID des Identity-Users)
    public string CreatorId { get; set; } = string.Empty;
    public UserProfile? Creator { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigationseigenschaft für die Mitglieder
    public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
}


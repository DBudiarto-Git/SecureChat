using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SecureChat.Core;

namespace SecureChat.Infrastructure;

// Wir erben von IdentityDbContext, um alle Tabellen für Auth (User, Roles, Claims) bereitzustellen
public class ApplicationDbContext : IdentityDbContext
{
    // Unsere neue Profil-Tabelle im SQL Server registrieren
    public DbSet<UserProfile> UserProfiles { get; set; }
    public DbSet<Friendship> Friendships { get; set; }
    // Zu den anderen DbSets hinzufügen:
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<ChatGroup> ChatGroups { get; set; }
    public DbSet<GroupMember> GroupMembers { get; set; }
    public DbSet<SecureChat.Core.GroupMessage> GroupMessages { get; set; }


    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // WICHTIG: Die Basis-Logik von Identity aufrufen, sonst stürzt EF Core ab!
        base.OnModelCreating(builder);

        // 1. Konfiguration für UserProfile (Erweitert um IsActive)
        builder.Entity<UserProfile>().HasKey(p => p.Id);
        builder.Entity<UserProfile>().Property(p => p.DisplayName).HasMaxLength(50).IsRequired();
        builder.Entity<UserProfile>().Property(p => p.PublicKeyXml).IsRequired(false);
        builder.Entity<UserProfile>().Property(p => p.IsActive).HasDefaultValue(true); // SQL Server Standardwert

        builder.Entity<UserProfile>().Property(p => p.ProfilePictureUrl).IsRequired(false).HasColumnType("nvarchar(max)"); // Ermöglicht das Speichern extrem großer Base64-Bilder!

        // 2. DATA SEEDING: Rollen für AspNetRoles definieren
        // IdentityRole verlangt eine feste ID (GUID), damit EF Core weiß, ob die Rolle bereits existiert
        var adminRoleId = "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d";
        var moderatorRoleId = "b2c3d4e5-f67a-8b9c-0d1e-2f3a4b5c6d7e";
        var userRoleId = "c3d4e5f6-7a8b-9c0d-1e2f-3a4b5c6d7e8f";

        builder.Entity< IdentityRole>().HasData(
            new IdentityRole
            {
                Id = adminRoleId,
                Name = "Admin",
                NormalizedName = "ADMIN"
            },
            new IdentityRole
            {
                Id = moderatorRoleId,
                Name = "Moderator",
                NormalizedName = "MODERATOR"
            },
            new IdentityRole
            {
                Id = userRoleId, // Für den Standard-User reicht der Name als Id oft aus, wir nutzen zur Konsistenz die ID
                Name = "User",
                NormalizedName = "USER"
            }
        );

        // Konfiguration für das Freundschafts-System
        builder.Entity<SecureChat.Core.Friendship>()
            .HasKey(f => f.Id);

        // Beziehung zum Sender
        builder.Entity<SecureChat.Core.Friendship>()
            .HasOne(f => f.Sender)
            .WithMany()
            .HasForeignKey(f => f.SenderId)
            .OnDelete(DeleteBehavior.Restrict); // Verhindert Kaskadenlöschung

        // Beziehung zum Empfänger
        builder.Entity<SecureChat.Core.Friendship>()
            .HasOne(f => f.Receiver)
            .WithMany()
            .HasForeignKey(f => f.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<SecureChat.Core.ChatMessage>()
            .HasKey(m => m.Id);

        builder.Entity<SecureChat.Core.ChatMessage>()
            .HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<SecureChat.Core.ChatMessage>()
            .HasOne(m => m.Receiver)
            .WithMany()
            .HasForeignKey(m => m.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ChatGroup>()
            .HasKey(g => g.Id);

        builder.Entity<ChatGroup>()
            .HasOne(g => g.Creator)
            .WithMany()
            .HasForeignKey(g => g.CreatorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<GroupMember>()
            .HasKey(m => m.Id);

        builder.Entity<GroupMember>()
            .HasOne(m => m.ChatGroup)
            .WithMany(g => g.Members)
            .HasForeignKey(m => m.ChatGroupId)
            .OnDelete(DeleteBehavior.Cascade); // Wenn die Gruppe gelöscht wird, fliegen die Mitglieder mit raus

        builder.Entity<GroupMember>()
            .HasOne(m => m.UserProfile)
            .WithMany()
            .HasForeignKey(m => m.UserProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<GroupMessage>()
            .HasKey(m => m.Id);

        builder.Entity<GroupMessage>()
            .HasOne(m => m.ChatGroup)
            .WithMany()
            .HasForeignKey(m => m.ChatGroupId)
            .OnDelete(DeleteBehavior.Cascade); // Wenn die Gruppe gelöscht wird, fliegen die Nachrichten mit raus

        builder.Entity<GroupMessage>()
            .HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}


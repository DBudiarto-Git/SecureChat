using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace SecureChat.Desktop.Models;

public class ContactModel : INotifyPropertyChanged
{
    private bool _isOnline;
    private bool _isFriend;
    private bool _isAlreadyInGroup;
    private string _displayName = string.Empty;
    private string _profilePictureBase64 = string.Empty;
    private BitmapImage? _avatarImage;

    public string Email { get; set; } = string.Empty;

    public string DisplayName
    {
        get => _displayName;
        set { _displayName = value; OnPropertyChanged(); OnPropertyChanged(nameof(Initials)); }
    }

    // Hier kommt der Base64-String aus der SQL-Datenbank an
    public string ProfilePictureBase64
    {
        get => _profilePictureBase64;
        set
        {
            _profilePictureBase64 = value;
            OnPropertyChanged();
            if (!string.IsNullOrEmpty(_profilePictureBase64))
            {
                AvatarImage = ConvertBase64ToBitmap(_profilePictureBase64);
            }
        }
    }

    // Das fertige Bildobjekt für das WPF-XAML
    public BitmapImage? AvatarImage
    {
        get => _avatarImage;
        set { _avatarImage = value; OnPropertyChanged(); }
    }

    public bool IsOnline
    {
        get => _isOnline;
        set { _isOnline = value; OnPropertyChanged(); }
    }

    public bool IsFriend
    {
        get => _isFriend;
        set { _isFriend = value; OnPropertyChanged(); }
    }

    public bool IsAlreadyInGroup
    {
        get => _isAlreadyInGroup;
        set { _isAlreadyInGroup = value; OnPropertyChanged(); }
    }

    //public int UnreadCount { get; set; } = 0;
    private int _unreadCount;
    public int UnreadCount
    {
        get => _unreadCount;
        set
        {
            _unreadCount = value;
            OnPropertyChanged(nameof(UnreadCount)); // 👈 WICHTIG: Das triggert das WPF-UI-Update!

            // 🌟 DIE RETTUNG: Informiert das XAML, dass sich die Sichtbarkeit geändert hat!
            OnPropertyChanged(nameof(HasUnread));
        }
    }

    // Hilfs-Property für das XAML (zeigt den Kreis nur an, wenn unread > 0)
    public bool HasUnread => UnreadCount > 0;

    public string Initials
    {
        get
        {
            if (string.IsNullOrWhiteSpace(DisplayName)) return "??";
            var parts = DisplayName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpper();
            return (parts[0].Substring(0, 1) + parts[1].Substring(0, 1)).ToUpper();
        }
    }

    private BitmapImage ConvertBase64ToBitmap(string base64String)
    {
        try
        {
            byte[] binaryData = Convert.FromBase64String(base64String);
            var bitmap = new BitmapImage();
            using (var stream = new MemoryStream(binaryData))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
            }
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null!;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}



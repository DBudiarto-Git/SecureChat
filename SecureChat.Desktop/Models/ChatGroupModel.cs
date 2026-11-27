using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SecureChat.Desktop.Models;

public class ChatGroupModel
{
    public int GroupId { get; set; }

    private string _groupName = string.Empty;
    

    public string GroupName
    {
        get => _groupName;
        set { _groupName = value; OnPropertyChanged(); }
    }

    private string _encryptedGroupKeyForMe = string.Empty;
    
    public string EncryptedGroupKeyForMe
    {
        get => _encryptedGroupKeyForMe;
        set { _encryptedGroupKeyForMe = value; OnPropertyChanged(); }
    }

    private string _iv = string.Empty;
    public string Iv
    {
        get => _iv;
        set { _iv = value; OnPropertyChanged(); }
    }


    private int _unreadCount;
    public int UnreadCount
    {
        get => _unreadCount;
        set
        {
            _unreadCount = value;
            OnPropertyChanged(nameof(UnreadCount)); // Aktualisiert die Zahl im XAML
            OnPropertyChanged(nameof(HasUnread));    // 🌟 Schaltet die Sichtbarkeit des Kreises!
        }
    }

    // Hilfs-Property für das XAML
    public bool HasUnread => UnreadCount > 0;


    // Standard WPF PropertyChanged-Mechanismus
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}


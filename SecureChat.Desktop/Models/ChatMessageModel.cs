using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace SecureChat.Desktop.Models;

public class ChatMessageModel : INotifyPropertyChanged
{
    private string _senderEmail = string.Empty;
    private string _receiverEmail = string.Empty;
    private string _cipherText = string.Empty;
    private string _messageType = "Text"; // "Text", "Image", "Audio", "Video", "File"
    private string? _fileName;
    private BitmapImage? _decryptedImagePayload;
    private byte[]? _decryptedFileBytes;
    private byte[]? _decryptedMediaBytes;
    private bool _isSender;

    // Dynamisches Icon für das XAML
    public string StatusIcon => MessageStatus >= 1 ? "✓✓" : "✓";

    // Dynamische Farbe für das XAML
    public string StatusColor
    {
        get
        {
            if (IsSender)
            {
                // 🔵 SENDER-SEITE (Auf blauem Hintergrund):
                // Status 2 = Leuchtendes Krypto-Cyan/Hellblau, sonst klares Weiß!
                return MessageStatus == 2 ? "#00E5FF" : "#FFFFFF";
            }
            else
            {
                // ⚪ EMPFÄNGER-SEITE (Auf weißem/grauem Hintergrund):
                // Status 2 = WhatsApp-Blau, sonst gut lesbares Dunkelgrau!
                return MessageStatus == 2 ? "#3498DB" : "#7F8C8D";
            }
        }
    }


    public string SenderEmail { get => _senderEmail; set { _senderEmail = value; OnPropertyChanged(); } }
    public string ReceiverEmail { get => _receiverEmail; set { _receiverEmail = value; OnPropertyChanged(); } }

    public string CipherText
    {
        get => _cipherText;
        set { _cipherText = value; OnPropertyChanged(); }
    }

    public string MessageType
    {
        get => _messageType;
        set { _messageType = value; OnPropertyChanged(); } // 🌟 Zwingt das XAML zum sofortigen Trigger-Umschalten!
    }

    public string? FileName { get => _fileName; set { _fileName = value; OnPropertyChanged(); } }
    public bool IsSender { get => _isSender; set { _isSender = value; OnPropertyChanged(); } }

    public BitmapImage? DecryptedImagePayload
    {
        get => _decryptedImagePayload;
        set { _decryptedImagePayload = value; OnPropertyChanged(); }
    }

    public byte[]? DecryptedFileBytes
    {
        get => _decryptedFileBytes;
        set { _decryptedFileBytes = value; OnPropertyChanged(); }
    }

    public byte[]? DecryptedMediaBytes
    {
        get => _decryptedMediaBytes;
        set { _decryptedMediaBytes = value; OnPropertyChanged(); }
    }

    // 1. Das private Feld für den Timer-Wert hinzufügen
    private double _audioProgressValue = 0;

    // 2. Das öffentliche Property mit UI-Benachrichtigung verankern
    public double AudioProgressValue
    {
        get => _audioProgressValue;
        set
        {
            _audioProgressValue = value;
            // Zwingt die ProgressBar im XAML dazu, live im selben Sekundenbruchteil mitzuwandern!
            OnPropertyChanged(nameof(AudioProgressValue));
        }
    }

    private string _playPauseIcon = "▶";
    public string PlayPauseIcon
    {
        get => _playPauseIcon;
        set { _playPauseIcon = value; OnPropertyChanged(); } // 🌟 Steuert ab jetzt NUR diese eine Textblase!
    }

    private string? _youTubeVideoId;
    public string? YouTubeVideoId
    {
        get => _youTubeVideoId;
        set { _youTubeVideoId = value; OnPropertyChanged(); }
    }

    private int _messageStatus = 0; // 0 = ✓, 1 = ✓✓ (grau), 2 = ✓✓ (blau)
    public int MessageStatus
    {
        get => _messageStatus;
        set
        {
            _messageStatus = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusIcon));
            OnPropertyChanged(nameof(StatusColor));

            // 🌟 DIE RETTUNG: Wir zwingen WPF, das visuelle Layout für diese Blase 
            // komplett neu zu evaluieren! Das bricht die Starre des YouTube-Steuerelements.
            OnPropertyChanged(nameof(IsSender));
        }
    }

    private int _id;
    public int Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); } // Live-Update für die Krypto-Weiche
    }

    public System.DateTime Timestamp { get; set; } = System.DateTime.UtcNow;

    // Für SignalR 1:1 Verschlüsselung
    public string EncryptedAesKey { get; set; } = string.Empty;
    public string Iv { get; set; } = string.Empty;
    public string AuthTag { get; set; } = string.Empty;

    // INotifyPropertyChanged-Implementierung
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

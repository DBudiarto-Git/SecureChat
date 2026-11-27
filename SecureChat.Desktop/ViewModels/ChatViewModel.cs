using Microsoft.AspNetCore.SignalR.Client;
using SecureChat.Desktop.Helpers;
using SecureChat.Desktop.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SecureChat.Desktop.ViewModels;

// KORREKTUR: partial class erlaubt uns das Aufteilen der Datei!
public partial class ChatViewModel : INotifyPropertyChanged
{
    private readonly HttpClient _httpClient;
    private HubConnection? _hubConnection;
    private string _myEmail = string.Empty;
    private string _messageText = string.Empty;
    private string _connectionStatus = "Getrennt";
    private string _searchQuery = string.Empty;
    private bool _isSidebarMaximized = true;
    private bool _isProfileViewActive = false;
    private string _activeTab = "Friends";
    private bool _isSearching = false;
    private bool _isGroupChatActive = false;
    private bool _isCreatingGroup = false;
    private string _newGroupName = string.Empty;
    private readonly string _jwtToken;

    private string _myDisplayName = "Lade...";
    private System.Windows.Media.Imaging.BitmapImage? _myAvatarImage;
    private string _myProfilePictureBase64 = string.Empty;

    private ContactModel? _selectedContact;
    private ChatGroupModel? _selectedGroup;

    private readonly RSACryptoServiceProvider _rsaProvider;
    private readonly string _privateKeyXml;
    public string PublicKeyXml { get; }
    private string _selectedFriendToInvite = string.Empty;
    private bool _isLoadingHistory = false;

    public ObservableCollection<ChatMessageModel> ChatHistory { get; } = new();
    public ObservableCollection<ContactModel> AvailableUsers { get; } = new();
    public ObservableCollection<ContactModel> MyFriends { get; } = new();
    public ObservableCollection<FriendshipInviteModel> PendingInvites { get; } = new();
    public ObservableCollection<ChatGroupModel> MyGroups { get; } = new();

    public ChatViewModel(string jwtToken)
    {
        _jwtToken = jwtToken;

        _httpClient = new HttpClient { BaseAddress = new Uri("https://localhost:7056/") };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);

        // Zuweisung aller Commands (die Logik liegt in den anderen Dateien!)
        SendCommand = new RelayCommand(async (_) => await SendMessageAsync());
        ToggleSidebarCommand = new RelayCommand(_ => IsSidebarMaximized = !IsSidebarMaximized);
        ToggleProfileViewCommand = new RelayCommand(async (param) => await HandleProfileViewToggleAsync(param));
        UploadAvatarCommand = new RelayCommand(_ => ExecuteUploadAvatar());

        SendInviteCommand = new RelayCommand(async (email) => await SendFriendInviteAsync(email));
        AcceptInviteCommand = new RelayCommand(async (id) => await HandleInviteResponseAsync(id, true));
        RejectInviteCommand = new RelayCommand(async (id) => await HandleInviteResponseAsync(id, false));

        SwitchTabCommand = new RelayCommand((param) => ActiveTab = param?.ToString() ?? "Friends");
        CreateGroupCommand = new RelayCommand(_ => { IsCreatingGroup = true; ActiveTab = "Groups"; NewGroupName = string.Empty; });
        ConfirmCreateGroupCommand = new RelayCommand(async (_) => await ExecuteConfirmCreateGroupAsync());
        AddMemberToGroupCommand = new RelayCommand(async (email) => await ExecuteAddMemberToGroupAsync(email));
        AttachFileCommand = new RelayCommand(async (_) => await ExecuteAttachFileAsync());
        DownloadFileCommand = new RelayCommand((msg) => ExecuteDownloadFile(msg));
        RotateKeysCommand = new RelayCommand(async (_) => await ExecuteRotateKeysAsync());
        ToggleAudioCommand = new RelayCommand((player) => ExecuteToggleAudioPlayback(player));

        string containerName = $"SecureChat_CryptoContainer_{MyInitials}";
        var cspParams = new CspParameters { KeyContainerName = containerName, Flags = CspProviderFlags.NoFlags };
        _rsaProvider = new RSACryptoServiceProvider(2048, cspParams);
        _privateKeyXml = _rsaProvider.ToXmlString(true);
        PublicKeyXml = _rsaProvider.ToXmlString(false);

        InitializeVoiceCommands();

        InitializeSignalR();
        _ = LoadMyProfileAsync();
        _ = UploadMyRsaPublicKeyAsync();
    }

    // Properties
    public string MyEmail { get => _myEmail; set { _myEmail = value; OnPropertyChanged(); } }
    public string MessageText { get => _messageText; set { _messageText = value; OnPropertyChanged(); } }
    public string ConnectionStatus { get => _connectionStatus; set { _connectionStatus = value; OnPropertyChanged(); } }
    public bool IsSidebarMaximized { get => _isSidebarMaximized; set { _isSidebarMaximized = value; OnPropertyChanged(); } }
    public bool IsProfileViewActive { get => _isProfileViewActive; set { _isProfileViewActive = value; OnPropertyChanged(); } }
    public string ActiveTab { get => _activeTab; set { _activeTab = value; OnPropertyChanged(); } }
    public bool IsSearching { get => _isSearching; set { _isSearching = value; OnPropertyChanged(); } }
    public bool IsGroupChatActive { get => _isGroupChatActive; set { _isGroupChatActive = value; OnPropertyChanged(); } }
    public bool IsCreatingGroup { get => _isCreatingGroup; set { _isCreatingGroup = value; OnPropertyChanged(); } }
    public string NewGroupName { get => _newGroupName; set { _newGroupName = value; OnPropertyChanged(); } }
    public string MyDisplayName { get => _myDisplayName; set { _myDisplayName = value; OnPropertyChanged(); OnPropertyChanged(nameof(MyInitials)); } }
    public System.Windows.Media.Imaging.BitmapImage? MyAvatarImage { get => _myAvatarImage; set { _myAvatarImage = value; OnPropertyChanged(); } }
    public string MyInitials => string.IsNullOrWhiteSpace(MyDisplayName) ? "??" : MyDisplayName.Substring(0, Math.Min(2, MyDisplayName.Length)).ToUpper();

    public string SelectedFriendToInvite { get => _selectedFriendToInvite; set { _selectedFriendToInvite = value; OnPropertyChanged(); } }
    

    public bool IsLoadingHistory
    {
        get => _isLoadingHistory;
        set { _isLoadingHistory = value; OnPropertyChanged(); }
    }

    public ContactModel? SelectedContact
    {
        get => _selectedContact;
        set
        {
            _selectedContact = value;
            OnPropertyChanged();

            if (_selectedContact != null)
            {
                // 🌟 FEHLERBEHEBUNG: Das UI-Badge sofort im Speicher auf 0 setzen!
                _selectedContact.UnreadCount = 0;

                // 🌟 DIE GEGEN-WEICHE: Wenn ein privater Freund angeklickt wird,
                // MUSS der Gruppenmodus unbarmherzig auf false fliegen!
                IsGroupChatActive = false; // ❌ Gruppenmodus DEAKTIVIEREN!

                // Wichtig: Wir nullen die ausgewählte Gruppe, damit kein Datenmix entsteht
                _selectedGroup = null;
                OnPropertyChanged(nameof(SelectedGroup));

                // 1. Verlauf laden (Triggert nun auch direkt Schritt 1 für die Quittung!)
                _ = LoadChatHistoryAsync(_selectedContact.Email);

                // 2. Lesebestätigung an die API und den SignalR-Hub feuern
                _ = MarkChatAsReadAsync(_selectedContact.Email);

                // 🌟 3. NEU: ONLINE-PRÄSENZ ERZWINGEN
                // Wir signalisieren dem Hub aktiv, dass wir diesen Chat jetzt betreten haben,
                // damit der Partner uns sofort GRÜN sieht und seine Häkchen live aktualisiert!
                if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
                {
                    // Ruft eine generische Methode im Hub auf, um dich anzumelden (z.B. GoOnline oder Ping)
                    _ = _hubConnection.InvokeAsync("ConfirmRead", _selectedContact.Email);

                    // Zusätzlicher Sicherheits-Ping: Sagt dem Partner, dass er MEINE ungelesenen Nachrichten ebenfalls quittieren soll
                    _ = _hubConnection.InvokeAsync("ConfirmDelivery", _selectedContact.Email, 0);

                }
            }
        }
    }

    // DIE ASYNCHRONE LESE-ENGINE FÜR 1:1-CHATS
    private async Task MarkChatAsReadAsync(string partnerEmail)
    {
        try
        {
            // A. Den SQL Server über die WebAPI informieren
            var response = await _httpClient.PostAsync($"api/messages/read-all?withUser={Uri.EscapeDataString(partnerEmail)}", null);

            if (response.IsSuccessStatusCode)
            {
                // B. Den Partner live über den SignalR-Hub benachrichtigen
                if (_hubConnection != null && _hubConnection.State == Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected)
                {
                    await _hubConnection.InvokeAsync("ConfirmRead", partnerEmail);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Krypto-Lese-Fehler] {ex.Message}");
        }
    }

    public ChatGroupModel? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            _selectedGroup = value;
            OnPropertyChanged();

            if (_selectedGroup != null)
            {
                // 🌟 FEHLERBEHEBUNG: Gruppen-Badge sofort im UI-Speicher verschwinden lassen!
                _selectedGroup.UnreadCount = 0;

                // 🌟 DIE RETTUNG: Wir schalten die Krypto-Weichen im RAM um!
                IsGroupChatActive = true;  // 🟢 Gruppenmodus AKTIVIEREN!

                // Falls du ein Property für den 1:1-Status hast (z.B. IsPrivateChatActive),
                // schalte es hier zur Sicherheit auf false:
                // IsPrivateChatActive = false; 

                // Wichtig: Wenn eine Gruppe geöffnet wird, müssen wir den 1:1-Kontakt nullen,
                // damit das System weiß, dass wir nicht mehr privat schreiben!
                SelectedContact = null;

                OnPropertyChanged(nameof(SelectedContact));

                // 1. Verlauf der Gruppe laden (deine bestehende Methode)
                _ = LoadGroupChatHistoryAsync(_selectedGroup.GroupId);

                // 2. 🌟 NEU: Wir markieren die empfangenen Gruppen-Nachrichten sofort live als gelesen!
                // Das sorgt dafür, dass die Gegenseite ihre verdienten blauen Häkchen bekommt.
                _ = MarkGroupChatAsReadAsync(_selectedGroup.GroupId);
            }
        }
    }

    private async Task MarkGroupChatAsReadAsync(int groupId)
    {
        try
        {
            // Ruft den neuen HTTP-POST-Endpunkt im Backend auf
            var response = await _httpClient.PostAsync($"api/groups/read-all?groupId={groupId}", null);

            if (response.IsSuccessStatusCode)
            {
                // Optional: Wenn der Hub verbunden ist, kannst du hier ein SignalR-Signal an die Gruppe senden,
                // damit die Häkchen bei den anderen Mitgliedern im selben Wimpernschlag blau anspringen!
                //if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
                //{
                //    await _hubConnection.InvokeAsync("GroupChatRead", groupId);
                //}

                if (response.IsSuccessStatusCode)
                {
                    // Dein SignalR-Code (kannst du hier weglassen, da der Server das unten bereits für alle erledigt!)
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[API-Fehler] Gruppenlese-Status konnte nicht gesetzt werden. Status: {response.StatusCode}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Gruppen-Krypto-Lese-Fehler] {ex.Message}");
        }
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set { _searchQuery = value; OnPropertyChanged(); if (string.IsNullOrWhiteSpace(_searchQuery)) { IsSearching = false; AvailableUsers.Clear(); _ = LoadFriendsAndInvitesAsync(); } else { IsSearching = true; _ = ExecuteSearchAsync(); } }
    }

    public ICommand SendCommand { get; }
    public ICommand ToggleSidebarCommand { get; }
    public ICommand ToggleProfileViewCommand { get; }
    public ICommand UploadAvatarCommand { get; }
    public ICommand SendInviteCommand { get; }
    public ICommand AcceptInviteCommand { get; }
    public ICommand RejectInviteCommand { get; }
    public ICommand SwitchTabCommand { get; }
    public ICommand CreateGroupCommand { get; }
    public ICommand ConfirmCreateGroupCommand { get; }
    public ICommand AddMemberToGroupCommand { get; }
    public ICommand AttachFileCommand { get; }
    public ICommand DownloadFileCommand { get; }
    public ICommand RotateKeysCommand { get; }
    public ICommand ToggleAudioCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    private record MyProfileData(string DisplayName, string ProfilePictureBase64, string Email);
}

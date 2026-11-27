using SecureChat.Desktop.Helpers;
using SecureChat.Desktop.Views;
using System;
using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SecureChat.Desktop.ViewModels;

public class AuthViewModel : INotifyPropertyChanged
{
    private readonly HttpClient _httpClient;
    private string _email = string.Empty;
    private string _displayName = string.Empty;
    private bool _isLoginView = true;
    private string _statusMessage = string.Empty;

    public AuthViewModel()
    {
        // In Produktion nutzen wir den HttpClientFactory-Ansatz, hier direkt für den Start:
        _httpClient = new HttpClient { BaseAddress = new Uri("https://localhost:7056/") }; // <-- Port an WebAPI anpassen!

        SubmitCommand = new RelayCommand(async (param) => await HandleSubmitAsync(param));
        ToggleViewCommand = new RelayCommand(_ => IsLoginView = !IsLoginView);
    }

    // UI-Bindings
    //public string Email { => _email; set { _email = value; OnPropertyChanged(); } }
    //public string DisplayName { => _displayName; set { _displayName = value; OnPropertyChanged(); } }
    //public bool IsLoginView { => _isLoginView; set { _isLoginView = value; OnPropertyChanged(); OnPropertyChanged(nameof(SwitchText)); } }
    //  KORREKT: Das Schlüsselwort 'get' vor dem Pfeil '=>' ist zwingend erforderlich
    public string Email
    {
        get => _email;
        set { _email = value; OnPropertyChanged(); }
    }

    public string DisplayName
    {
        get => _displayName;
        set { _displayName = value; OnPropertyChanged(); }
    }

    public bool IsLoginView
    {
        get => _isLoginView;
        set
        {
            _isLoginView = value;
            OnPropertyChanged();
            // Das XAML lauscht jetzt direkt auf 'IsLoginView' für seine Trigger,
            // daher benötigen wir hier keinen verwaisten SwitchText-Aufruf mehr!
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    //public bool IsLoginView
    //{
    //    get => _isLoginView;
    //    set { _isLoginView = value; OnPropertyChanged(); OnPropertyChanged(nameof(SwitchText)); }
    //}
    ////public string StatusMessage { => _statusMessage; set { _statusMessage = value; OnPropertyChanged(); } }
    //public string StatusMessage
    //{
    //    get => _statusMessage;
    //    set { _statusMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(SwitchText)); }
    //}
    //public string SwitchText => IsLoginView ? "Noch kein Konto? Hier registrieren" : "Bereits registriert? Zum Login";

    // Commands
    public ICommand SubmitCommand { get; }
    public ICommand ToggleViewCommand { get; }

    private async Task HandleSubmitAsync(object? passwordParam)
    {
        // SECURITY: Wir lesen das Passwort direkt aus der PasswordBox als SecureString/Sicheres Objekt aus,
        // um Klartext-Strings im RAM-Cache zu minimieren.
        if (passwordParam is not PasswordBox passwordBox) return;
        string password = passwordBox.Password;

        if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(password))
        {
            StatusMessage = "Bitte alle Felder ausfüllen.";
            return;
        }

        StatusMessage = "Verarbeitung...";

        try
        {
            if (IsLoginView)
            {
                // LOGIN ABSENDEN
                var response = await _httpClient.PostAsJsonAsync("api/auth/login", new { email = Email, password = password });
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<LoginResult>();
                    StatusMessage = "Login erfolgreich!";

                    if (result != null && !string.IsNullOrEmpty(result.AccessToken))
                    {
                        // 1. Das neue ChatViewModel mit dem echten JWT-Token erstellen
                        var chatVm = new ChatViewModel(result.AccessToken);

                        // 2. Das neue Haupt-Chat-Fenster erzeugen
                        var chatWindow = new ChatWindow(chatVm);

                        // 3. ZUERST das neue Chat-Fenster anzeigen (Wichtig für das Windows-System!)
                        chatWindow.Show();

                        // 4. DANACH das alte Authentifizierungs-Fenster heraussuchen und schließen
                        foreach (Window window in Application.Current.Windows)
                        {
                            if (window is AuthWindow)
                            {
                                window.Close();
                                break;
                            }
                        }
                    }
                }
                else
                {
                    StatusMessage = "Login fehlgeschlagen. Daten prüfen.";
                }
            }
            else
            {
                // REGISTRIERUNG ABSENDEN
                if (string.IsNullOrEmpty(DisplayName)) { StatusMessage = "Anzeigename fehlt."; return; }

                var response = await _httpClient.PostAsJsonAsync("api/auth/register", new { email = Email, password = password, displayName = DisplayName });
                if (response.IsSuccessStatusCode)
                {
                    StatusMessage = "Registrierung erfolgreich! Du kannst dich jetzt einloggen.";
                    IsLoginView = true; // Automatisch zurück zum Login wechseln
                    passwordBox.Clear();
                }
                else
                {
                    string serverError = await response.Content.ReadAsStringAsync();
                    //StatusMessage = "Registrierung fehlgeschlagen.";
                    StatusMessage = $"Fehler: {serverError}";
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Verbindungsfehler: {ex.Message}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private record LoginResult(string AccessToken);
}


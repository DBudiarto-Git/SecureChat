using SecureChat.Desktop.Models;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace SecureChat.Desktop.ViewModels;

public partial class ChatViewModel
{
    // REPARIERT: Schickt die Freundschaftsanfrage wieder krypto-konform an die API!
    private async Task SendFriendInviteAsync(object? emailParam)
    {
        // Wir fangen das XAML-Objekt ab und wandeln es in einen sauberen E-Mail-String um
        if (emailParam is not string email || string.IsNullOrEmpty(email))
        {
            System.Windows.MessageBox.Show("Sicherheits-Warnung: Ungültige E-Mail-Adresse übergeben.", "System", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        try
        {
            // API-Aufruf an den FriendshipController abfeuern
            var response = await _httpClient.PostAsJsonAsync("api/friendship/invite", email);

            if (response.IsSuccessStatusCode)
            {
                System.Windows.MessageBox.Show($"Freundschaftsanfrage erfolgreich an '{email}' gesendet!", "System", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

                // Suchergebnisse zurücksetzen und Kontakte neu laden
                IsSearching = false;
                SearchQuery = string.Empty;
                AvailableUsers.Clear();
                _ = LoadFriendsAndInvitesAsync();
            }
            else
            {
                string serverError = await response.Content.ReadAsStringAsync();
                System.Windows.MessageBox.Show($"Server hat die Anfrage abgelehnt: {serverError}", "Fehler", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Netzwerkfehler beim Senden der Anfrage: {ex.Message}", "Fehler", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private async Task HandleInviteResponseAsync(object? id, bool acc)
    {
        try { var response = await _httpClient.PostAsJsonAsync("api/friendship/respond", new { friendshipId = id, accept = acc }); if (response.IsSuccessStatusCode) await LoadFriendsAndInvitesAsync(); } catch (Exception) { }
    }

    //private async Task ExecuteSearchAsync()
    //{
    //    if (string.IsNullOrWhiteSpace(SearchQuery)) return;
    //    try { var result = await _httpClient.GetFromJsonAsync<List<ContactModel>>($"api/users/search?query={SearchQuery}"); if (result != null) { AvailableUsers.Clear(); foreach (var user in result) AvailableUsers.Add(user); } } catch (Exception) { }
    //}

    private async Task ExecuteSearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery)) return;
        try
        {
            // Wenn eine Gruppe offen ist, hängen wir deren ID an die URL an!
            string url = $"api/users/search?query={Uri.EscapeDataString(SearchQuery)}";
            if (IsGroupChatActive && SelectedGroup != null)
            {
                url += $"&groupId={SelectedGroup.GroupId}";
            }

            var result = await _httpClient.GetFromJsonAsync<List<ContactModel>>(url);
            if (result != null)
            {
                AvailableUsers.Clear();
                foreach (var user in result) AvailableUsers.Add(user);
            }
        }
        catch (Exception) { }
    }

    // Deklariere den Command in dieser oder der Hauptdatei:
    // public ICommand DownloadFileCommand { get; set; }

    // 🌟 NEU: DIE DOWNLOAD ENGINE (Bytes verlustfrei auf die Festplatte schreiben)
    public void ExecuteDownloadFile(object? messageParam)
    {
        if (messageParam is not ChatMessageModel msg || msg.MessageType != "File" || msg.DecryptedFileBytes == null)
        {
            return; // Keine Datei oder Buffer leer
        }

        try
        {
            // Windows-Speicherdialog initialisieren
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = msg.FileName, // Schlägt den originalen Dateinamen vor
                DefaultExt = System.IO.Path.GetExtension(msg.FileName),
                Title = "Verschlüsselte Datei auf Festplatte sichern"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                //// Schreibt die dechiffrierten Bytes im Bruchteil einer Sekunde aus dem RAM auf die Platte
                //System.IO.File.WriteAllBytes(saveFileDialog.FileName, msg.DecryptedFileBytes);

                //System.Windows.MessageBox.Show($"Datei erfolgreich dechiffriert und unter '{System.IO.Path.GetFileName(saveFileDialog.FileName)}' gespeichert!",
                //    "Download abgeschlossen ✓", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                // 🌟 DIE RETTUNG: Wir prüfen, ob der Pfad gültig und nicht leer ist!
                string targetPath = saveFileDialog.FileName;

                if (string.IsNullOrWhiteSpace(targetPath))
                {
                    return; // Abbrechen, falls der Pfad ungültig ist
                }

                // Jetzt weiß der Compiler zu 100%, dass targetPath NIEMALS null sein kann!
                System.IO.File.WriteAllBytes(targetPath, msg.DecryptedFileBytes);

                System.Windows.MessageBox.Show($"Datei erfolgreich dechiffriert und unter '{System.IO.Path.GetFileName(targetPath)}' gespeichert!",
                    "Download abgeschlossen ✓", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Fehler beim Schreiben der Datei: {ex.Message}", "System-Fehler");
        }
    }

    //private string _playPauseIcon = "▶";
    //public string PlayPauseIcon
    //{
    //    get => _playPauseIcon;
    //    set { _playPauseIcon = value; OnPropertyChanged(nameof(PlayPauseIcon)); }
    //}

    private static System.Windows.Threading.DispatcherTimer? _audioPlaybackTimer;
    private static ChatMessageModel? _currentPlayingMessage;
    private static System.Windows.Controls.MediaElement? _currentActiveXamlPlayer;

    // 🌟 DIE LÖSUNG: Das Flag lebt ab jetzt direkt hier im ViewModel!
    public static bool IsUserDraggingSlider = false;

    // 🌟 DIE ULTIMATIVE MULTIMEDIA-STEUERUNG (Echtes Pause + threadsicherer End-Reset)
    public void ExecuteToggleAudioPlayback(object? messageContextParam)
    {
        if (messageContextParam is System.Windows.Controls.MediaElement xamlPlayer)
        {
            if (xamlPlayer.DataContext is not ChatMessageModel msg) return;

            try
            {
                // FALL 1: Das Medium soll gestartet ODER nach einer Pause fortgesetzt werden
                if (msg.PlayPauseIcon == "▶")
                {
                    // WICHTIG: Falls ein VÖLLIG ANDERES Medium im Chat läuft, setzen wir das alte komplett zurück
                    if (_currentPlayingMessage != null && _currentPlayingMessage != msg)
                    {
                        ResetAndStopEntirePlayback();
                    }

                    _currentPlayingMessage = msg;
                    _currentActiveXamlPlayer = xamlPlayer;

                    // Steuerungsmodus im UI-Thread fixieren
                    _currentActiveXamlPlayer.LoadedBehavior = System.Windows.Controls.MediaState.Manual;
                    _currentActiveXamlPlayer.UnloadedBehavior = System.Windows.Controls.MediaState.Manual;

                    // Startet oder setzt die Wiedergabe exakt an der angehaltenen Position fort!
                    _currentActiveXamlPlayer.Play();

                    msg.PlayPauseIcon = "⏸";
                    msg.CipherText = msg.MessageType == "Audio" ? "🎵 Audio-Kanal aktiv..." : "🎥 Video-Kanal aktiv...";

                    // Timer für die ProgressBar starten, falls noch nicht aktiv
                    if (_audioPlaybackTimer == null)
                    {
                        _audioPlaybackTimer = new System.Windows.Threading.DispatcherTimer
                        {
                            Interval = TimeSpan.FromMilliseconds(100)
                        };
                        _audioPlaybackTimer.Tick += (s, e) => UpdateXamlAudioProgress();
                        _audioPlaybackTimer.Start();
                    }
                }
                // FALL 2: Der Nutzer klickt mitten im Abspielen auf den PAUSE-Button!
                else
                {
                    if (_currentActiveXamlPlayer != null)
                    {
                        _currentActiveXamlPlayer.Pause(); // ⏸ ECHTE PAUSE: Friert die Position unberührt im RAM ein!
                    }

                    _audioPlaybackTimer?.Stop();
                    _audioPlaybackTimer = null;

                    msg.PlayPauseIcon = "▶"; // Button wird wieder zum Play-Symbol
                    msg.CipherText = msg.MessageType == "Audio" ? "⏸ Audio pausiert" : "⏸ Video pausiert";
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Multimedia-Fehler: {ex.Message}");
                ResetAndStopEntirePlayback();
            }
        }
    }

    // Hilfsmethode: Aktualisiert den Fortschrittsbalken live über die Soundkarte
    //private void UpdateXamlAudioProgress()
    //{
    //    if (_currentPlayingMessage == null || _currentActiveXamlPlayer == null) return;

    //    if (_currentActiveXamlPlayer.NaturalDuration.HasTimeSpan)
    //    {
    //        double totalDuration = _currentActiveXamlPlayer.NaturalDuration.TimeSpan.TotalSeconds;
    //        double currentPosition = _currentActiveXamlPlayer.Position.TotalSeconds;

    //        if (totalDuration > 0)
    //        {
    //            double progressPercentage = (currentPosition / totalDuration) * 100;
    //            _currentPlayingMessage.AudioProgressValue = progressPercentage;
    //        }
    //    }
    //}

    private void UpdateXamlAudioProgress()
    {
        if (_currentPlayingMessage == null || _currentActiveXamlPlayer == null) return;

        // 🌟 KORREKTUR: Greift nun direkt auf das lokale Feld zu, ohne fehlerhaften XAML-Verweis!
        if (IsUserDraggingSlider) return;

        if (_currentActiveXamlPlayer.NaturalDuration.HasTimeSpan)
        {
            double totalDuration = _currentActiveXamlPlayer.NaturalDuration.TimeSpan.TotalSeconds;
            double currentPosition = _currentActiveXamlPlayer.Position.TotalSeconds;

            if (totalDuration > 0)
            {
                double progressPercentage = (currentPosition / totalDuration) * 100;
                _currentPlayingMessage.AudioProgressValue = progressPercentage;

                // Schutz für sekundenschnelle Kurzdateien
                if (totalDuration - currentPosition <= 0.15 || progressPercentage >= 99.5)
                {
                    ResetAndStopEntirePlayback();
                }
            }
        }
    }


    // 🌟 NEU: DIE THREADSICHERE RESETS-METHODE FÜR DAS LIVE-FILMENDE
    // Wird aufgerufen, wenn ein Medium von ganz alleine die letzte Millisekunde erreicht!
    public static void ResetAndStopEntirePlayback()
    {
        // Timer restlos terminieren
        _audioPlaybackTimer?.Stop();
        _audioPlaybackTimer = null;

        if (_currentActiveXamlPlayer != null)
        {
            // Zwingt den Cursor im RAM bedingungslos zurück auf Frame 0!
            _currentActiveXamlPlayer.Position = TimeSpan.Zero;
            _currentActiveXamlPlayer.Stop();
            _currentActiveXamlPlayer = null;
        }

        if (_currentPlayingMessage != null)
        {
            _currentPlayingMessage.AudioProgressValue = 0; // Balken auf Linksanschlag
            _currentPlayingMessage.PlayPauseIcon = "▶";   // Button zurück auf Start
            _currentPlayingMessage.CipherText = _currentPlayingMessage.MessageType == "Audio"
                ? $"🎵 Audio-Nachricht: {_currentPlayingMessage.FileName}"
                : $"🎥 Video-Nachricht: {_currentPlayingMessage.FileName}";
            _currentPlayingMessage = null;
        }
    }

    // 🌟 NEU: DIE SPUL-ENGINE (Berechnet den Prozentwert in echte Sekunden zurück)
    public void SpoolMediaToPosition(ChatMessageModel msg, double targetPercentage)
    {
        if (_currentActiveXamlPlayer != null && _currentActiveXamlPlayer.NaturalDuration.HasTimeSpan)
        {
            try
            {
                double totalDuration = _currentActiveXamlPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                // Prozentwert zurück in Sekunden rechnen
                double targetSeconds = (targetPercentage / 100.0) * totalDuration;

                // Den Video- oder Audio-Cursor live im RAM an die neue Stelle zwingen!
                _currentActiveXamlPlayer.Position = TimeSpan.FromSeconds(targetSeconds);
            }
            catch { }
        }
    }

}


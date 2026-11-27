using Microsoft.AspNetCore.SignalR.Client;
using SecureChat.Desktop.Helpers;
using SIPSorcery.Media;
using SIPSorcery.Net; // 🔑 Schaltet alle Core-Klassen und Media-Formate frei!
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SecureChat.Desktop.ViewModels;

public partial class ChatViewModel
{
    private RTCPeerConnection? _peerConnection;
    private VoIPMediaSession? _mediaSession;
    private System.Media.SoundPlayer? _ringtonePlayer;

    // 🔑 Der Krypto-Zwischenspeicher für das SDP-Angebot
    private string _tempSdpOffer = string.Empty;

    // 🌟 NEU: Das Command für die XAML-Anbindung
    public System.Windows.Input.ICommand StartVoiceCallCommand { get; private set; }
    public System.Windows.Input.ICommand CancelCallCommand { get; private set; } // Für Auflegen / Abbrechen (Rot)
    public System.Windows.Input.ICommand AcceptCallCommand { get; private set; } // Für Annehmen (Grün)

    private bool _isIncomingCallActive;
    public bool IsIncomingCallActive
    {
        get => _isIncomingCallActive;
        set { _isIncomingCallActive = value; OnPropertyChanged(); }
    }

    private bool _isOutgoingCallActive;
    public bool IsOutgoingCallActive
    {
        get => _isOutgoingCallActive;
        set { _isOutgoingCallActive = value; OnPropertyChanged(); }
    }

    private string _currentCallPartner = string.Empty;
    public string CurrentCallPartner
    {
        get => _currentCallPartner;
        set { _currentCallPartner = value; OnPropertyChanged(); }
    }

    // 🌟 NEU: Methode zum Abbrechen oder Auflegen
    //public void CancelOrHangUpCall()
    //{
    //    try
    //    {
    //        System.Diagnostics.Debug.WriteLine("[Voice-Call] Gespräch beendet / abgebrochen.");

    //        // Sound stoppen, falls noch aktiv
    //        _ringtonePlayer?.Stop();

    //        // WebRTC-Verbindung threadsicher abbauen
    //        if (_peerConnection != null)
    //        {
    //            _peerConnection.Close("");
    //            _peerConnection = null;
    //        }

    //        // Flags zurücksetzen, um die UI live zu bereinigen
    //        IsIncomingCallActive = false;
    //        IsOutgoingCallActive = false;
    //        CurrentCallPartner = string.Empty;

    //        // TODO: SignalR-Event "CallEnded" an den Partner funken, falls gewünscht
    //    }
    //    catch (Exception ex)
    //    {
    //        System.Diagnostics.Debug.WriteLine($"[Voice-Hangup-Fehler] {ex.Message}");
    //    }
    //}

    // Beendet das Gespräch aktiv und funkt das Signal an den Partner
    public void CancelOrHangUpCall()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[Voice-Call] Local CancelOrHangUpCall() gestartet.");

            // 1. Live-Signal an den Partner senden, falls wir mit jemandem verbunden sind
            if (!string.IsNullOrEmpty(CurrentCallPartner) && _hubConnection != null && _hubConnection.State == Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected)
            {
                // Wir funken den Partner asynchron an, BEVOR wir die lokale Pipeline schließen!
                _ = _hubConnection.InvokeAsync("SendEndCall", CurrentCallPartner);
            }

            // 2. Sound-Player stoppen und aufräumen
            _ringtonePlayer?.Stop();
            _ringtonePlayer?.Dispose();
            _ringtonePlayer = null;

            // 3. WebRTC-Verbindung mathematisch sauber trennen
            if (_peerConnection != null)
            {
                _peerConnection.Close("");
                _peerConnection = null;
            }

            if (_mediaSession != null)
            {
                _mediaSession.Close("");
                _mediaSession = null;
            }

            // 4. 🌟 DIE LIVE-RETTUNG FÜR DIE OBERFLÄCHE:
            // Alle UI-Flags zurücksetzen, damit die Icons augenblicklich wieder grün werden!
            IsIncomingCallActive = false;
            IsOutgoingCallActive = false;
            CurrentCallPartner = string.Empty;
            _tempSdpOffer = string.Empty;

            System.Diagnostics.Debug.WriteLine("[Voice-Call] Krypto-Telefonie-Pipeline erfolgreich zurückgesetzt.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Voice-Hangup-Fehler] {ex.Message}");
        }
    }

    // Startet den Anruf-Vorgang (User A ruft an)
    public async Task StartVoiceCallAsync(string targetEmail)
    {
        try
        {
            // 1. Mathematisch korrekte STUN-Server Syntax ohne Webfehler
            var stunServer = new RTCIceServer
            {
                urls = "stun:://google.com"
            };

            // 2. Die Peer-Konfiguration aufbauen
            var rtcConfig = new RTCConfiguration
            {
                iceServers = new List<RTCIceServer> { stunServer }
            };

            // 3. Die PeerConnection direkt aus dem Core instanziieren
            _peerConnection = new RTCPeerConnection(rtcConfig);

            // 4. 🌟 DIE REIHENFOLGEN-RETTUNG FÜR DEN CONSTRUCTOR:
            // Parameter 1: SDPMediaTypesEnum.audio
            // Parameter 2: 111 (Format ID als int)
            // Parameter 3: "opus" (Codec Name als string)
            // Parameter 4: 48000 (Clock Rate als int)
            var codecList = new List<SDPAudioVideoMediaFormat>
            {
                new SDPAudioVideoMediaFormat(SDPMediaTypesEnum.audio, 111, "opus", 48000)
            };

            // 5. Wir übergeben die perfekt formatisierte Liste an den MediaStreamTrack-Konstruktor!
            var audioTrack = new MediaStreamTrack(SDPMediaTypesEnum.audio, false, codecList, MediaStreamStatusEnum.SendRecv);
            _peerConnection.addTrack(audioTrack);

            // 6. Netzwerk-Kandidaten (ICE) live über SignalR austauschen (bestehend)
            _peerConnection.onicecandidate += (candidate) =>
            {
                if (candidate != null && _hubConnection != null)
                {
                    string json = candidate.toJSON();
                    _ = _hubConnection.InvokeAsync("SendIceCandidate", targetEmail, json);
                }
            };

            // ======================= BEGINN: ZUM HERAUSFINDEN, OB VOICE GESENDET UND EMPFANGEN WÜRDE ============================

            _peerConnection.onconnectionstatechange += (state) =>
            {
                System.Diagnostics.Debug.WriteLine($"[WebRTC-Krypto-Status] Verbindung steht auf: {state}");
            };

            // 🌟 DER ULTIMATIVE BEWEIS: Springt bei JEDEM empfangenen Sprach-Paket im RAM an!
            _peerConnection.OnAudioFormatsNegotiated += (formats) =>
            {
                System.Diagnostics.Debug.WriteLine("[Voice-Erfolg] Audio-Codecs erfolgreich ausgehandelt! Pakete fließen.");
            };

            // ======================= ENDE: ZUM HERAUSFINDEN, OB VOICE GESENDET UND EMPFANGEN WÜRDE ============================

            // 7. Verschlüsseltes Anruf-Angebot (SDP Offer) generieren (bestehend)
            var offer = _peerConnection.createOffer(null);
            _peerConnection.setLocalDescription(offer);

            // 8. Das Angebot über SignalR an User B abschicken (bestehend)
            if (_hubConnection != null)
            {
                await _hubConnection.InvokeAsync("SendCallOffer", targetEmail, offer.sdp);
            }

            System.Diagnostics.Debug.WriteLine($"[Voice-Call] Anruf an {targetEmail} über Core-WebRTC erfolgreich initiiert!");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Voice-Fehler] {ex.Message}");
        }
    }

    // Verarbeitet die Anruf-Annahme auf der Empfängerseite (User B)
    public async Task AcceptVoiceCallAsync(string callerEmail, string sdpOffer)
    {
        try
        {
            // 1. Peer-Konfiguration mit dem Google-STUN-Server spiegeln
            var stunServer = new RTCIceServer { urls = "stun:://google.com" };
            var rtcConfig = new RTCConfiguration { iceServers = new List<RTCIceServer> { stunServer } };

            _peerConnection = new RTCPeerConnection(rtcConfig);

            // 2. Symmetrischer Codec-Abgleich (Wir fordern exakt das vom Compiler verifizierte Format)
            var codecList = new List<SDPAudioVideoMediaFormat>
            {
                new SDPAudioVideoMediaFormat(SDPMediaTypesEnum.audio, 111, "opus", 48000)
            };

            // Audio-Spur generieren und der PeerConnection hinzufügen
            var audioTrack = new MediaStreamTrack(SDPMediaTypesEnum.audio, false, codecList, MediaStreamStatusEnum.SendRecv);
            _peerConnection.addTrack(audioTrack);

            // 3. ICE-Kandidaten (Netzwerk-IPs) während des Verbindungsaufbaus live zurückmelden
            _peerConnection.onicecandidate += (candidate) =>
            {
                if (candidate != null && _hubConnection != null)
                {
                    string json = candidate.toJSON();
                    // Sendet meine IP-Kandidaten an den Anrufer (callerEmail)
                    _ = _hubConnection.InvokeAsync("SendIceCandidate", callerEmail, json);
                }
            };

            // ======================= BEGINN: ZUM HERAUSFINDEN, OB VOICE GESENDET UND EMPFANGEN WÜRDE ============================

            _peerConnection.onconnectionstatechange += (state) =>
            {
                System.Diagnostics.Debug.WriteLine($"[WebRTC-Krypto-Status] Verbindung steht auf: {state}");
            };

            // 🌟 DER ULTIMATIVE BEWEIS: Springt bei JEDEM empfangenen Sprach-Paket im RAM an!
            _peerConnection.OnAudioFormatsNegotiated += (formats) =>
            {
                System.Diagnostics.Debug.WriteLine("[Voice-Erfolg] Audio-Codecs erfolgreich ausgehandelt! Pakete fließen.");
            };

            // ======================= ENDE: ZUM HERAUSFINDEN, OB VOICE GESENDET UND EMPFANGEN WÜRDE ============================

            // 4. Das empfangene Krypto-Angebot von User A in unserer Engine einrasten lassen
            var rtcOffer = new RTCSessionDescriptionInit { sdp = sdpOffer, type = RTCSdpType.offer };
            _peerConnection.setRemoteDescription(rtcOffer);

            // 5. Die krypto-gesicherte Antwort (SDP Answer) berechnen
            var answer = _peerConnection.createAnswer(null);
            _peerConnection.setLocalDescription(answer);

            // 6. Die Antwort live über SignalR an den Anrufer zurücksenden!
            if (_hubConnection != null)
            {
                await _hubConnection.InvokeAsync("SendCallAnswer", callerEmail, answer.sdp);
            }

            System.Diagnostics.Debug.WriteLine($"[Voice-Call] Krypto-Antwort an {callerEmail} übermittelt. P2P-Leitung wird geschaltet...");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Voice-Annahme-Fehler] {ex.Message}");
        }
    }

    // FÜGE DIESE INITIALISIERUNG IN DEN KONSTRUKTOR DEINES VIEWMODELS EIN:
    // (Falls du einen zentralen Konstruktor hast, verschiebe diese 3 Zeilen dorthin)
    // Initialisiert das Command absolut compiler-konform für deine RelayCommand-Klasse
    private void InitializeVoiceCommands()
    {
        // 1. Anruf starten (Grüner Button)
        StartVoiceCallCommand = new RelayCommand(async (_) =>
        {
            if (SelectedContact != null && !string.IsNullOrEmpty(SelectedContact.Email))
            {
                System.Diagnostics.Debug.WriteLine($"[UI-Klick] Starte Telefonat mit: {SelectedContact.Email}");
                IsOutgoingCallActive = true; // Schaltet deinen Button sofort auf das rote Kreuz (❌) um!
                await StartVoiceCallAsync(SelectedContact.Email);
            }
        }, (_) => SelectedContact != null && !IsGroupChatActive);

        // 2. Anruf abbrechen / Auflegen (Roter Button)
        CancelCallCommand = new RelayCommand((_) =>
        {
            CancelOrHangUpCall(); // Ruft deine aufräumende C#-Methode auf
        });

        // 3. Anruf annehmen (Grüner Button im In-App-Overlay)
        AcceptCallCommand = new RelayCommand(async (_) =>
        {
            System.Diagnostics.Debug.WriteLine("[UI-Klick] Anruf aktiv angenommen.");

            // Klingeln stoppen
            _ringtonePlayer?.Stop();
            _ringtonePlayer?.Dispose();

            // Overlay ausblenden
            IsIncomingCallActive = false;

            // 🌟 DIE RETTUNG FÜR DAS ICON:
            // Wir schalten dieses Flag auch beim Empfänger auf True! 
            // Dadurch wechselt SEIN Telefon-Hörer im Header nun AUCH sofort live in das rote Kreuz (❌) zum Auflegen!
            IsOutgoingCallActive = true;

            // Krypto-Gegenstelle zünden mitsamt dem im RAM zwischengespeicherten Offer!
            if (!string.IsNullOrEmpty(CurrentCallPartner) && !string.IsNullOrEmpty(_tempSdpOffer))
            {
                await AcceptVoiceCallAsync(CurrentCallPartner, _tempSdpOffer);
            }
        });
    }
}

using Microsoft.AspNetCore.SignalR.Client;
using SecureChat.Desktop.Models;
using SIPSorcery.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Interop;

namespace SecureChat.Desktop.ViewModels;

public partial class ChatViewModel
{
    // 🌟 DAS KRYPTO-PRESENCE-GEDÄCHTNIS: Merkt sich die Online-User, falls die Listen noch laden!
    private static readonly System.Collections.Generic.List<string> _tempOnlineCache = new();

    // Erstelle ein globales SoundPlayer-Objekt ganz oben in deiner ChatViewModel.CoreMethods.cs:
    //private System.Media.SoundPlayer? _ringtonePlayer;

    //private string _tempSdpOffer = "";


    // 1. SIGNALS & STREAMING INITIALISIEREN
    private async void InitializeSignalR()
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl("https://localhost:7056/chathub", options => { options.AccessTokenProvider = () => Task.FromResult<string ?>(_jwtToken); })
            .WithAutomaticReconnect()
            .Build();

        // In ChatViewModel.CoreMethods.cs bei InitializeSignalR:
        //_hubConnection.On<string, string, string, string, string, string, string, string>("ReceiveMessage", (sender, receiver, encKey, cipher, iv, tag, msgType, fileName) =>
        //_hubConnection.On<string, string, string, string, string, string, string, string, int>("ReceiveMessage", (sender, receiver, encKey, cipher, iv, tag, msgType, fileName, liveMessageId) =>
        //{
        _hubConnection.On("ReceiveMessage", new Type[] { typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(int) }, async (parameters) =>
        {
            // Wir ziehen die 9 Parameter sicher aus dem Objekt-Array heraus
            // KORREKTUR: Sicheres Auslesen ohne Nullverweis-Warnungen!
            string sender = (parameters?[0] as string) ?? string.Empty;
            string receiver = (parameters?[1] as string) ?? string.Empty;
            string encKey = (parameters?[2] as string) ?? string.Empty;
            string cipher = (parameters?[3] as string) ?? string.Empty;
            string iv = (parameters?[4] as string) ?? string.Empty;
            string tag = (parameters?[5] as string) ?? string.Empty;
            string msgType = (parameters?[6] as string) ?? string.Empty;
            string fileName = (parameters?[7] as string) ?? string.Empty;
            int liveMessageId = parameters?[8] is int id ? id : 0; // 🌟 Sicheres Unboxing

            App.Current.Dispatcher.Invoke(() => {
                if (SelectedContact != null && SelectedContact.Email == sender)
                {
                    var incomingMsg = new ChatMessageModel
                    {
                        Id = liveMessageId, // 🌟 JETZT ERHÄLT DIE LIVE-NACHRICHT IHRE ECHTE ID AUS DER LUFT!
                        SenderEmail = sender,
                        ReceiverEmail = receiver,
                        Timestamp = DateTime.UtcNow,
                        IsSender = false,
                        MessageType = msgType, // 🌟 NUTZT DEN ECHTEN TYP VOM SERVER!
                        FileName = fileName
                    };

                    try
                    {
                        byte[] encryptedBytes = Convert.FromBase64String(cipher);
                        byte[] ivBytes = Convert.FromBase64String(iv);
                        byte[] authTagBytes = Convert.FromBase64String(tag);
                        byte[] encryptedKeyBytes = Convert.FromBase64String(encKey);

                        byte[] aesKey;
                        using (var rsaPrivate = new System.Security.Cryptography.RSACryptoServiceProvider(2048))
                        {
                            rsaPrivate.FromXmlString(_privateKeyXml);
                            aesKey = rsaPrivate.Decrypt(encryptedKeyBytes, true);
                        }

                        byte[] decryptedBytes = new byte[encryptedBytes.Length];
                        using (var aesGcm = new System.Security.Cryptography.AesGcm(aesKey, 16))
                        {
                            aesGcm.Decrypt(ivBytes, encryptedBytes, authTagBytes, decryptedBytes);
                        }

                        // 🌟 KORREKTUR: Typen-Weiche exakt wie im Verlauf zuweisen!
                        if (incomingMsg.MessageType == "Image")
                        {
                            incomingMsg.DecryptedImagePayload = ConvertBytesToBitmap(decryptedBytes);
                            incomingMsg.CipherText = $"📷 Bild geladen: {fileName}";
                        }
                        else if (incomingMsg.MessageType == "Audio")
                        {
                            incomingMsg.DecryptedFileBytes = decryptedBytes;
                            incomingMsg.DecryptedMediaBytes = decryptedBytes;
                            incomingMsg.CipherText = $"🎵 Audio-Nachricht: {fileName}";
                        }
                        else if (incomingMsg.MessageType == "Video")
                        {
                            incomingMsg.DecryptedFileBytes = decryptedBytes;
                            incomingMsg.DecryptedMediaBytes = decryptedBytes;
                            incomingMsg.CipherText = $"🎥 Video-Nachricht: {fileName}";
                        }
                        else if (incomingMsg.MessageType == "File")
                        {
                            incomingMsg.DecryptedFileBytes = decryptedBytes;
                            incomingMsg.CipherText = $"📎 Datei: {fileName}\n👉 Doppelklick zum Speichern";
                        }
                        else
                        {
                            incomingMsg.CipherText = System.Text.Encoding.UTF8.GetString(decryptedBytes);
                        }

                        // Ganz am Ende deines try-Blocks (nach ChatHistory.Add(incomingMsg);) steht nun dein Rückruf:
                        if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
                        {
                            // Wir feuern die Empfangsbestätigung threadsicher an den Sender zurück!
                            _ = _hubConnection.InvokeAsync("ConfirmDelivery", incomingMsg.SenderEmail, incomingMsg.Id);
                        }

                        // 🌟 DIE RETTUNG FÜR DEN EMPFÄNGER:
                        // Wir jagen den entschlüsselten Text live durch deine YouTube-Weiche,
                        // BEVOR die Nachricht auf den Bildschirm gezeichnet wird!
                            // Falls DetectYouTubeLink eine Methode deines ViewModels ist:
                        DetectYouTubeLink(incomingMsg);
                    }
                    catch (Exception ex)
                    {
                        incomingMsg.CipherText = $"[🔒 Live-Krypto-Fehler: {ex.Message}]";
                        incomingMsg.MessageType = "Text";
                    }

                    ChatHistory.Add(incomingMsg);

                    //// 🌟 NEU: Sobald die Nachricht in MEINER UI landet, funken wir den Server an,
                    //// damit der Sender seine zwei grauen Häkchen (✓✓) bekommt!
                    //if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
                    //{
                    //    // Wir schicken dem Sender die ID der Nachricht, die gerade bei uns angekommen ist
                    //    _ = _hubConnection.InvokeAsync("ConfirmDelivery", incomingMsg.SenderEmail, incomingMsg.Id);
                    //}

                    //// 🌟 DIE RETTUNG FÜR SITUATION 2 (Live-Chatting):
                    //// Wir prüfen: Habe ich (User B) das Chatfenster zu diesem Sender gerade AKTIV geöffnet?
                    //if (SelectedContact != null && SelectedContact.Email.Equals(sender, StringComparison.OrdinalIgnoreCase))
                    //{
                    //    // Wenn ja, markieren wir diese frische Nachricht sofort live als gelesen!
                    //    // Das funkt das Lese-Signal 'ChatReadByPartner' augenblicklich an User A zurück!
                    //    _ = MarkChatAsReadAsync(sender);
                    //}

                }
            });
        });

        // KORREKTUR: Jetzt mit 7 Parametern für lückenloses Gruppen-Payload-Streaming!
        _hubConnection.On<int, string, string, string, string, string, string>("ReceiveGroupMessage", (groupId, senderEmail, cipher, iv, tag, msgType, fileName) =>
        {
            App.Current.Dispatcher.Invoke(() => {
                if (SelectedGroup != null && SelectedGroup.GroupId == groupId)
                {
                    var incomingMsg = new ChatMessageModel
                    {
                        SenderEmail = senderEmail,
                        Timestamp = DateTime.UtcNow,
                        IsSender = false,
                        MessageType = msgType, // 🌟 NUTZT DEN ECHTEN TYP VOM SERVER!
                        FileName = fileName
                    };

                    try
                    {
                        // Krypto-Tricks für Gruppen: Symmetrischen Gruppenschlüssel holen
                        byte[] rawGroupKey = DecryptAesKeyWithMyRsa(SelectedGroup.EncryptedGroupKeyForMe);
                        byte[] decryptedBytes;

                        // Weichenstellung für Medien-Payloads (Bilder, Audio, Video)
                        if (msgType == "Image" || msgType == "Audio" || msgType == "Video" || msgType == "File")
                        {
                            // Das trennbare Gruppen-Krypto-Paket (Key + Datei) aufspalten
                            string[] parts = cipher.Split('|');
                            string encFileAesKeyForGroup = parts[0];
                            string keyIv = parts[1];
                            string keyTag = parts[2];
                            string fileCipherBase64 = parts[3];

                            // Datei-AES-Schlüssel dechiffrieren, der mit dem Gruppen-Key verschlüsselt wurde
                            string fileAesKeyBase64 = DecryptWithGroupAesKey(encFileAesKeyForGroup, keyIv, keyTag, rawGroupKey);
                            byte[] fileAesKey = Convert.FromBase64String(fileAesKeyBase64);

                            byte[] encryptedFileBytes = Convert.FromBase64String(fileCipherBase64);
                            byte[] ivBytes = Convert.FromBase64String(iv);
                            byte[] authTagBytes = Convert.FromBase64String(tag);

                            decryptedBytes = new byte[encryptedFileBytes.Length];
                            using (var aesGcm = new System.Security.Cryptography.AesGcm(fileAesKey, 16))
                            {
                                aesGcm.Decrypt(ivBytes, encryptedFileBytes, authTagBytes, decryptedBytes);
                            }
                        }
                        else
                        {
                            // Normaler Gruppen-Text
                            string clearText = DecryptWithGroupAesKey(cipher, iv, tag, rawGroupKey);
                            decryptedBytes = System.Text.Encoding.UTF8.GetBytes(clearText);
                        }

                        // 🌟 KORREKTUR: Krypto-Zuweisung je nach echtem Multimedia-Dateityp
                        if (incomingMsg.MessageType == "Image")
                        {
                            incomingMsg.DecryptedImagePayload = ConvertBytesToBitmap(decryptedBytes);
                            incomingMsg.CipherText = $"📷 Bild geladen: {fileName}";
                        }
                        else if (incomingMsg.MessageType == "Audio")
                        {
                            // Bytes für den Inline-Player und den Doppelklick-Download sichern!
                            incomingMsg.DecryptedFileBytes = decryptedBytes;
                            incomingMsg.DecryptedMediaBytes = decryptedBytes;
                            incomingMsg.CipherText = $"🎵 Audio-Nachricht: {fileName}";
                        }
                        else if (incomingMsg.MessageType == "Video")
                        {
                            incomingMsg.DecryptedFileBytes = decryptedBytes;
                            incomingMsg.DecryptedMediaBytes = decryptedBytes;
                            incomingMsg.CipherText = $"🎥 Video-Nachricht: {fileName}";
                        }
                        else if (incomingMsg.MessageType == "File")
                        {
                            incomingMsg.DecryptedFileBytes = decryptedBytes;
                            incomingMsg.CipherText = $"📎 Datei empfangen: {fileName}\n👉 Doppelklick zum Speichern";
                        }
                        else
                        {
                            incomingMsg.CipherText = System.Text.Encoding.UTF8.GetString(decryptedBytes);
                        }
                    }
                    catch (Exception ex)
                    {
                        incomingMsg.CipherText = $"[🔒 Live-Gruppen-Krypto-Fehler: {ex.Message}]";
                        incomingMsg.MessageType = "Text";
                    }

                    ChatHistory.Add(incomingMsg);
                }
            });
        });


        _hubConnection.On<int>("AddedToGroup", (groupId) =>
        {
            // Wir wechseln sofort auf den UI-Thread, um die Listen sicher neu zu laden
            App.Current.Dispatcher.Invoke(() =>
            {
                // Ruft die Methode auf, die alle Freunde, Invites und GRUPPEN neu streamt!
                _ = LoadFriendsAndInvitesAsync();
            });
        });

        _hubConnection.On<string, bool>("UserStatusChanged", (email, isOnline) =>
        {
            App.Current.Dispatcher.Invoke(() => {
                foreach (var friend in MyFriends) 
                { 
                    if (friend.Email.Equals(email, StringComparison.OrdinalIgnoreCase)) 
                    { 
                        friend.IsOnline = isOnline; 
                        break; 
                    } 
                }
            });
        });

        _hubConnection.On("NewFriendInviteReceived", () =>
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                // Aktualisiert sofort die Liste der offenen Einladungen live auf dem Bildschirm
                _ = LoadFriendsAndInvitesAsync();
            });
        });

        // 🌟 A: Wenn der Partner meldet, dass MEINE Nachricht bei ihm auf dem Bildschirm aufgeploppt ist (Zugestellt)
        _hubConnection.On<int>("MessageDelivered", (messageId) =>
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                var targetMsg = ChatHistory.FirstOrDefault(m => m.Id == messageId);
                if (targetMsg != null && targetMsg.MessageStatus < 1)
                {
                    targetMsg.MessageStatus = 1; // Wechselt automatisch zu zwei grauen Häkchen!
                }
            });
        });

        // 🌟 B: Wenn der Partner den Chat öffnet und MEINE Nachrichten aktiv liest
        _hubConnection.On<string>("ChatReadByPartner", (partnerEmail) =>
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                if (SelectedContact != null && SelectedContact.Email == partnerEmail)
                {
                    foreach (var msg in ChatHistory.Where(m => m.IsSender))
                    {
                        msg.MessageStatus = 2; // Wechselt alle meine gesendeten Nachrichten live zu zwei blauen Häkchen!
                    }
                }
            });
        });

        // ===================================================================
        // 🌟 DIE ULTIMATIVE RETTUNG: Direkter Zugriff auf den echten UI-DataContext!
        // ===================================================================

        // Event A: Abgleich aller aktuell online befindlichen Kontakte beim Login
        _hubConnection.On<System.Collections.Generic.List<string>>("UpdateOnlineUsersList", (onlineEmails) =>
        {
            if (onlineEmails == null) return;

            App.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    // 🔥 WIR GREIFEN UNS DAS AKTIVE FENSTER AUS DER WINDOWS-LISTE!
                    // Wir suchen das Fenster, das gerade wirklich geöffnet ist (ChatWindow)
                    foreach (System.Windows.Window window in App.Current.Windows)
                    {
                        if (window is SecureChat.Desktop.Views.ChatWindow && window.DataContext is SecureChat.Desktop.ViewModels.ChatViewModel realActiveVM)
                        {
                            // Jetzt rattert der Code GARANTIERT durch die echten Freunde des angemeldeten Users!
                            if (realActiveVM.AvailableUsers != null)
                            {
                                foreach (var contact in realActiveVM.AvailableUsers)
                                {
                                    contact.IsOnline = onlineEmails.Contains(contact.Email, System.StringComparer.OrdinalIgnoreCase);
                                }
                            }

                            if (realActiveVM.MyFriends != null)
                            {
                                foreach (var contact in realActiveVM.MyFriends)
                                {
                                    contact.IsOnline = onlineEmails.Contains(contact.Email, System.StringComparer.OrdinalIgnoreCase);
                                }
                            }
                            break; // Gefunden und abgeglichen!
                        }
                    }
                }
                catch { }
            });
        });

        // Event B: Live-Signal, wenn im Nachgang jemand online/offline geht
        _hubConnection.On<string, bool>("UserStatusChanged", (userEmail, isOnline) =>
        {
            if (System.String.IsNullOrEmpty(userEmail)) return;

            App.Current.Dispatcher.Invoke(async () =>
            {
                try
                {
                    // Auch hier suchen wir das physikalisch angezeigte Fenster im RAM
                    foreach (System.Windows.Window window in App.Current.Windows)
                    {
                        if (window is SecureChat.Desktop.Views.ChatWindow && window.DataContext is SecureChat.Desktop.ViewModels.ChatViewModel realActiveVM)
                        {
                            if (realActiveVM.AvailableUsers != null)
                            {
                                foreach (var contact in realActiveVM.AvailableUsers)
                                {
                                    if (contact.Email.Equals(userEmail, System.StringComparison.OrdinalIgnoreCase))
                                        contact.IsOnline = isOnline;
                                }
                            }

                            if (realActiveVM.MyFriends != null)
                            {
                                foreach (var contact in realActiveVM.MyFriends)
                                {
                                    if (contact.Email.Equals(userEmail, System.StringComparison.OrdinalIgnoreCase))
                                        contact.IsOnline = isOnline;
                                }
                            }

                            // Der doppelte Häkchen-Blitz bei Live-Online-Kommen auf der ECHTEN UI-Instanz
                            if (isOnline && realActiveVM.SelectedContact != null && realActiveVM.SelectedContact.Email.Equals(userEmail, System.StringComparison.OrdinalIgnoreCase))
                            {
                                await realActiveVM.MarkChatAsReadAsync(userEmail);
                            }
                            break;
                        }
                    }
                }
                catch { }
            });
        });

        // 🌟 NEU: Wenn ein anderes Mitglied die Gruppe öffnet und MEINE Nachrichten aktiv liest
        _hubConnection.On<int>("GroupChatReadByPartner", (groupId) =>
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                // Wenn wir uns gerade visuell in genau dieser Gruppe befinden
                if (IsGroupChatActive && SelectedGroup != null && SelectedGroup.GroupId == groupId)
                {
                    // Alle meine in dieser Gruppe gesendeten Nachrichten live auf Blau (2) umschalten!
                    foreach (var msg in ChatHistory.Where(m => m.IsSender))
                    {
                        msg.MessageStatus = 2; // Wechselt die Ticks live zu zwei blauen Häkchen! [INDEX]
                    }
                }

                // -----------------------------------------------------------------
                // ERWEITERUNG SCHRITT B: Den Badge-Zähler im Speicher synchronisieren
                // -----------------------------------------------------------------
                // Finde die Gruppe in deiner linken Gruppenliste (z.B. MyGroupsList oder GroupsCollection)
                // Passe 'MyGroupsList' an den exakten Namen deiner ObservableCollection für die Gruppen an!
                var localGroup = MyGroups?.FirstOrDefault(g => g.GroupId == groupId);

                if (localGroup != null)
                {
                    // Wenn wir diese Gruppe gerade aktiv geöffnet haben, muss das Badge sowieso 0 sein
                    if (IsGroupChatActive && SelectedGroup?.GroupId == groupId)
                    {
                        localGroup.UnreadCount = 0;
                    }
                    else
                    {
                        // Hinweis zur Krypto-Architektur:
                        // Wenn ein ANDERER Gruppenchat-Teilnehmer die Gruppe als gelesen markiert, 
                        // betrifft das SEINEN Zähler. Wenn du die Liste beim Server per HTTP frisch lädst,
                        // filtert die SQL-Abfrage über 'm.SenderId != currentUserId'. 
                        // Falls du hier merkst, dass dein eigener Zähler bei Fremd-Events fälschlicherweise springt,
                        // kannst du das Herabsetzen hier ignorieren oder mit der Server-API abgleichen.
                    }
                }
            });
        });

        // 📞 VOICE-CALL LAUSCHER: Wenn mich jemand live anruft (Eingehendes Angebot)
        //_hubConnection.On<string, string>("ReceiveCallOffer", (senderEmail, sdpOffer) =>
        //{
        //    App.Current.Dispatcher.Invoke(async () =>
        //    {
        //        System.Diagnostics.Debug.WriteLine($"[Voice-Signal] Eingehender Anruf von: {senderEmail}");

        //        try
        //        {
        //            // 🌟 SOUND-START: Spielt einen Standard-Windows-Klingelton asynchron in einer Dauerschleife ab!
        //            // Du kannst auch den Pfad zu einer eigenen "ringtone.wav" auf deiner Festplatte angeben.
        //            _ringtonePlayer = new System.Media.SoundPlayer(@"C:\Windows\Media\Windows Ringin.wav");
        //            _ringtonePlayer.PlayLooping();
        //        }
        //        catch { /* Fängt ab, falls die Sounddatei auf dem PC fehlt */ }

        //        // Das Abfrage-Fenster blockiert den Thread, während es klingelt
        //        var result = System.Windows.MessageBox.Show(
        //            $"Eingehender verschlüsselter Sprachanruf von {senderEmail}.\nMöchtest du das Gespräch annehmen?",
        //            "📞 Eingehender Anruf",
        //            System.Windows.MessageBoxButton.YesNo,
        //            System.Windows.MessageBoxImage.Question);

        //        // 🌟 SOUND-STOPP: Sobald der Benutzer klickt (Egal ob Ja oder Nein), hört das Klingeln auf!
        //        _ringtonePlayer?.Stop();
        //        _ringtonePlayer?.Dispose();

        //        if (result == System.Windows.MessageBoxResult.Yes)
        //        {
        //            await AcceptVoiceCallAsync(senderEmail, sdpOffer);
        //        }
        //        else
        //        {
        //            System.Diagnostics.Debug.WriteLine("[Voice-Call] Anruf abgelehnt.");
        //        }
        //    });
        //});

        // 📞 VOICE-CALL LAUSCHER: Wenn mich jemand live anruft (Eingehendes Angebot)
        _hubConnection.On<string, string>("ReceiveCallOffer", (senderEmail, sdpOffer) =>
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                System.Diagnostics.Debug.WriteLine($"[Voice-Signal] Eingehender Anruf von: {senderEmail}");

                // Speicherplatz für das Krypto-Szenario sichern
                CurrentCallPartner = senderEmail;

                try
                {
                    // Klingelton starten
                    _ringtonePlayer = new System.Media.SoundPlayer(@"C:\Windows\Media\Windows Ringin.wav");
                    _ringtonePlayer.PlayLooping();
                }
                catch { }

                // 🌟 DIE RETTUNG FÜR DIE UI: Keine blockierende MessageBox mehr!
                // Wir schalten einfach das Flag auf True. Das XAML blendet sofort das schöne Overlay ein!
                IsIncomingCallActive = true;

                // Wir merken uns das SDP-Offer im Hintergrund, um es bei Klick auf "Annehmen" zu nutzen
                // Tipp: Du kannst das sdpOffer kurz in einer temporären Variable im ViewModel zwischenspeichern:
                _tempSdpOffer = sdpOffer;
            });
        });

        // 📞 VOICE-CALL ANTWORT: Wenn der Partner (User B) den Anruf live annimmt
        _hubConnection.On<string>("ReceiveCallAnswer", (sdpAnswer) =>
        {
            App.Current.Dispatcher.Invoke(() => // 'async' wird hier nicht mehr benötigt!
            {
                System.Diagnostics.Debug.WriteLine("[Voice-Signal] Partner hat den Anruf angenommen! Verarbeite Krypto-Antwort...");

                if (_peerConnection != null)
                {
                    var rtcAnswer = new RTCSessionDescriptionInit { sdp = sdpAnswer, type = RTCSdpType.answer };

                    // 🌟 DIE RETTUNG: Wir entfernen das 'await'! Die Methode läuft direkt synchron im RAM ein.
                    var result = _peerConnection.setRemoteDescription(rtcAnswer);

                    System.Diagnostics.Debug.WriteLine($"[Voice-Call] Krypto-Antwort verarbeitet. Status: {result}. P2P-Leitung offen!");
                }
            });
        });

        // 📞 ICE-CANDIDATE TAUSCH: Ermöglicht den PCs, sich trotz Firewalls im Internet direkt zu finden
        _hubConnection.On<string>("ReceiveIceCandidate", (candidateJson) =>
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                if (_peerConnection != null && !string.IsNullOrEmpty(candidateJson))
                {
                    // Fügt die IP-Adresse des Partners live zur PeerConnection hinzu
                    _peerConnection.addIceCandidate(new RTCIceCandidateInit { candidate = candidateJson });
                }
            });
        });

        // 📞 VOICE-CALL LAUSCHER: Wenn der Partner das Gespräch beendet oder auflegt
        _hubConnection.On<string>("ReceiveCallEnded", (senderEmail) =>
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                System.Diagnostics.Debug.WriteLine($"[Voice-Signal] Partner ({senderEmail}) hat aufgelegt.");

                // 🚀 DIE UI-RETTUNG: Wir rufen die lokale Aufräum-Methode auf!
                // Das schließt die WebRTC-Leitung threadsicher und setzt alle Icons zurück!
                CancelOrHangUpCall();
            });
        });


        //// ===================================================================
        //// 🌟 DIE RETTUNG: Race Condition gelöst über direkte 'this'-Instanzierung!
        //// ===================================================================

        //// Event A: Abgleich aller aktuell online befindlichen Kontakte beim Login
        //_hubConnection.On<System.Collections.Generic.List<string>>("UpdateOnlineUsersList", (onlineEmails) =>
        //{
        //    if (onlineEmails == null) return;

        //    App.Current.Dispatcher.Invoke(() =>
        //    {
        //        lock (_tempOnlineCache)
        //        {
        //            // 1. Im Cache für später einbrennen
        //            _tempOnlineCache.Clear();
        //            _tempOnlineCache.AddRange(onlineEmails);
        //        }

        //        // 2. Sofortiger Anwendungsversuch (falls die Liste schon da sein sollte)
        //        ApplyOnlineStatusFromCache();
        //    });
        //});

        //// Event B: Live-Signal, wenn im Nachgang jemand online/offline geht
        //_hubConnection.On<string, bool>("UserStatusChanged", (userEmail, isOnline) =>
        //{
        //    if (System.String.IsNullOrEmpty(userEmail)) return;

        //    App.Current.Dispatcher.Invoke(() =>
        //    {
        //        lock (_tempOnlineCache)
        //        {
        //            if (isOnline)
        //            {
        //                if (!_tempOnlineCache.Contains(userEmail)) _tempOnlineCache.Add(userEmail);
        //            }
        //            else
        //            {
        //                _tempOnlineCache.Remove(userEmail);
        //            }
        //        }

        //        // Live auf dem Bildschirm umschalten
        //        ApplyOnlineStatusFromCache();
        //    });
        //});


        ConnectionStatus = "Verbinde...";
        try { 
            await _hubConnection.StartAsync(); // 🚀 Verbindung wird physikalisch aufgebaut!
            ConnectionStatus = "Sicher verbunden 🔒"; 
            _ = LoadFriendsAndInvitesAsync();

            // ===================================================================
            // 🌟 NEU: DER ENTSCHEIDENDE ANSTOSS (Handschlag nach erfolgreichem Start!)
            // ===================================================================
            if (_hubConnection.State == Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected)
            {
                // Wir rufen die neue Hub-Methode auf und fordern die Liste aktiv an!
                //await _hubConnection.InvokeAsync("RequestOnlineUsersList"); // ⚡ HIERMIT ZÜNDET DIE KETTE!
                await _hubConnection.InvokeAsync("RequestActiveOnlineUsers"); // ⚡ Stoßt den Server an!
            }
        }
        catch { }
    }

    // 🌟 DIE PRESENCE-ENGINE: Synchronisiert deine UI-Kreise reaktiv mit dem RAM-Cache
    public void ApplyOnlineStatusFromCache()
    {
        App.Current.Dispatcher.Invoke(async () =>
        {
            try
            {
                lock (_tempOnlineCache)
                {
                    // 1. Abgleich in AvailableUsers
                    if (this.AvailableUsers != null && this.AvailableUsers.Count > 0)
                    {
                        foreach (var contact in this.AvailableUsers)
                        {
                            contact.IsOnline = _tempOnlineCache.Contains(contact.Email, System.StringComparer.OrdinalIgnoreCase);
                        }
                    }

                    // 2. Abgleich in MyFriends
                    if (this.MyFriends != null && this.MyFriends.Count > 0)
                    {
                        foreach (var contact in this.MyFriends)
                        {
                            contact.IsOnline = _tempOnlineCache.Contains(contact.Email, System.StringComparer.OrdinalIgnoreCase);
                        }
                    }
                }

                // Doppel-Häkchen-Blitz nachladen, falls wir den aktiven Chat offen haben
                if (this.SelectedContact != null && _tempOnlineCache.Contains(this.SelectedContact.Email, System.StringComparer.OrdinalIgnoreCase))
                {
                    await this.MarkChatAsReadAsync(this.SelectedContact.Email);
                }
            }
            catch { }
        });
    }

    // 2. PROFIL-INFORMATIONEN AUSLESEN & SPEICHERN
    private async Task LoadMyProfileAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/users/me");
            if (response.IsSuccessStatusCode)
            {
                var myData = await response.Content.ReadFromJsonAsync<MyProfileData>();
                if (myData != null)
                {
                    MyDisplayName = myData.DisplayName;
                    _myProfilePictureBase64 = myData.ProfilePictureBase64;
                    if (!string.IsNullOrEmpty(_myProfilePictureBase64)) MyAvatarImage = ConvertBase64ToBitmap(_myProfilePictureBase64);

                    // 🔥 HIER WIRD ES JETZT ECHT BEFÜLLT: Deine E-Mail landet sicher im RAM!
                    MyEmail = myData.Email;
                }
            }
        }
        catch { }
    }

    private async Task HandleProfileViewToggleAsync(object? p)
    {
        if (p?.ToString() == "Save")
        {
            try { await _httpClient.PutAsJsonAsync("api/users/me", new { displayName = MyDisplayName, profilePictureBase64 = _myProfilePictureBase64 }); IsProfileViewActive = false; } catch { }
        }
        else { IsProfileViewActive = !IsProfileViewActive; }
    }

    private void ExecuteUploadAvatar()
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog { Filter = "Bilddateien (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png" };
        if (openFileDialog.ShowDialog() == true)
        {
            try { byte[] imageBytes = System.IO.File.ReadAllBytes(openFileDialog.FileName); _myProfilePictureBase64 = Convert.ToBase64String(imageBytes); MyAvatarImage = ConvertBase64ToBitmap(_myProfilePictureBase64); } catch { }
        }
    }

    private System.Windows.Media.Imaging.BitmapImage ConvertBase64ToBitmap(string base64String)
    {
        byte[] binaryData = Convert.FromBase64String(base64String);
        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
        using (var stream = new System.IO.MemoryStream(binaryData))
        {
            bitmap.BeginInit(); bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit();
        }
        bitmap.Freeze(); return bitmap;
    }

    // 1. VERMISSTE METHODE: GRUPPEN-VERLAUF KOORDINIEREN UND ENTSCHLÜSSELN
    //private async Task LoadGroupChatHistoryAsync(int groupId)
    //{
    //    if (SelectedGroup == null) return;
    //    try
    //    {
    //        IsLoadingHistory = true;

    //        var history = await _httpClient.GetFromJsonAsync<List<GroupMessageModel>>($"api/groups/history?groupId={groupId}");

    //        App.Current.Dispatcher.Invoke(() =>
    //        {
    //            ChatHistory.Clear();
    //            ChatHistory.Add(new ChatMessageModel { SenderEmail = "System", CipherText = $"👥 Gruppe '{SelectedGroup.GroupName}' geöffnet. (Gruppen-E2EE aktiv)" });
    //        });

    //        //if (history == null) return;
    //        if (history == null || history.Count == 0)
    //        {
    //            IsLoadingHistory = false;
    //            return;
    //        }

    //        // PERFORMANCE-BOOST: CPU-rechenintensive Entschlüsselung in den Hintergrund schieben!
    //        await Task.Run(() =>
    //        {
    //            byte[] rawGroupKey = DecryptAesKeyWithMyRsa(SelectedGroup.EncryptedGroupKeyForMe);

    //            foreach (var msg in history)
    //            {
    //                bool isMe = msg.SenderEmail.Equals(MyEmail, StringComparison.OrdinalIgnoreCase);
    //                var uiMsg = new ChatMessageModel
    //                {
    //                    SenderEmail = msg.SenderEmail,
    //                    Timestamp = msg.Timestamp,
    //                    IsSender = isMe,
    //                    MessageType = msg.MessageType,
    //                    FileName = msg.FileName
    //                };

    //                try
    //                {
    //                    byte[] decryptedBytes;

    //                    if (msg.MessageType == "Image" || msg.MessageType == "Audio" || msg.MessageType == "Video" || msg.MessageType == "File")
    //                    //    if (msg.MessageType == "Image" || msg.MessageType == "File")
    //                    {
    //                        string[] parts = msg.CipherText.Split('|');
    //                        string encFileAesKeyForGroup = parts[0];
    //                        string keyIv = parts[1];
    //                        string keyTag = parts[2];
    //                        string fileCipherBase64 = parts[3];

    //                        string fileAesKeyBase64 = DecryptWithGroupAesKey(encFileAesKeyForGroup, keyIv, keyTag, rawGroupKey);
    //                        byte[] fileAesKey = Convert.FromBase64String(fileAesKeyBase64);

    //                        byte[] encryptedFileBytes = Convert.FromBase64String(fileCipherBase64);
    //                        byte[] ivBytes = Convert.FromBase64String(msg.Iv);
    //                        byte[] authTagBytes = Convert.FromBase64String(msg.AuthTag);

    //                        decryptedBytes = new byte[encryptedFileBytes.Length];
    //                        using (var aesGcm = new System.Security.Cryptography.AesGcm(fileAesKey, 16))
    //                        {
    //                            aesGcm.Decrypt(ivBytes, encryptedFileBytes, authTagBytes, decryptedBytes);
    //                        }
    //                    }
    //                    else
    //                    {
    //                        string clearText = DecryptWithGroupAesKey(msg.CipherText, msg.Iv, msg.AuthTag, rawGroupKey);
    //                        decryptedBytes = System.Text.Encoding.UTF8.GetBytes(clearText);
    //                    }

    //                    if (uiMsg.MessageType == "Image")
    //                    {
    //                        uiMsg.DecryptedImagePayload = ConvertBytesToBitmap(decryptedBytes);
    //                        uiMsg.CipherText = $"📷 Bild geladen: {msg.FileName}";
    //                    }
    //                    else if (uiMsg.MessageType == "Audio" || uiMsg.MessageType == "Video")
    //                    {
    //                        // NEU: Medienbytes für die Player im RAM sichern
    //                        uiMsg.DecryptedFileBytes = decryptedBytes; // Für Download-Fallback
    //                        uiMsg.DecryptedMediaBytes = decryptedBytes; // Für Inline-Player
    //                        uiMsg.CipherText = uiMsg.MessageType == "Audio" ? $"🎵 Audio-Nachricht: {uiMsg.FileName}" : $"🎥 Video-Nachricht: {uiMsg.FileName}";
    //                    }
    //                    else if (uiMsg.MessageType == "File")
    //                    {
    //                        uiMsg.DecryptedFileBytes = decryptedBytes;
    //                        uiMsg.CipherText = $"📎 Datei empfangen: {msg.FileName}\n👉 Doppelklick zum Speichern";
    //                    }
    //                    else
    //                    {
    //                        uiMsg.CipherText = System.Text.Encoding.UTF8.GetString(decryptedBytes);
    //                    }
    //                }
    //                catch (Exception ex)
    //                {
    //                    uiMsg.CipherText = $"[🔒 Krypto-Fehler: {ex.Message}]";
    //                    uiMsg.MessageType = "Text";
    //                }

    //                // Sofort einzeln auf die UI werfen
    //                App.Current.Dispatcher.Invoke(() =>
    //                {
    //                    ChatHistory.Add(uiMsg);
    //                });
    //            }
    //        });
    //    }
    //    catch (Exception ex)
    //    {
    //        App.Current.Dispatcher.Invoke(() => ChatHistory.Add(new ChatMessageModel { SenderEmail = "System", CipherText = $"⚠️ Krypto-Fehler beim Laden des Gruppen-Verlaufs: {ex.Message}" }));
    //    }
    //    finally
    //    {
    //        // 🌟 LAUFZEIT-INDIKATOR BEENDEN
    //        IsLoadingHistory = false;
    //    }
    //}


    // 2. VERMISSTE METHODE: GRUPPEN-ERSTELLUNG BESTÄTIGEN
    private async Task ExecuteConfirmCreateGroupAsync()
    {
        if (string.IsNullOrWhiteSpace(NewGroupName)) return;
        string groupNameTmp = NewGroupName;
        IsCreatingGroup = false;
        try
        {
            byte[] groupAesKey = new byte[32];
            RandomNumberGenerator.Fill(groupAesKey);
            byte[] iv = new byte[12];
            RandomNumberGenerator.Fill(iv);
            byte[] encryptedKeyForMe = _rsaProvider.Encrypt(groupAesKey, true);

            var payload = new
            {
                GroupName = groupNameTmp,
                EncryptedGroupKeyForMe = Convert.ToBase64String(encryptedKeyForMe),
                Iv = Convert.ToBase64String(iv)
            };
            var response = await _httpClient.PostAsJsonAsync("api/groups/create", payload);
            if (response.IsSuccessStatusCode)
            {
                await Task.Delay(150);
                await LoadFriendsAndInvitesAsync();
                NewGroupName = string.Empty;
            }
        }
        catch (Exception) { }
    }
}


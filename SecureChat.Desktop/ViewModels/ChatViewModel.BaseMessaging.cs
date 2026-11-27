using Microsoft.AspNetCore.SignalR.Client;
using SecureChat.Desktop.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace SecureChat.Desktop.ViewModels;

public partial class ChatViewModel
{
    //// 1. CHAT-VERLAUF LADEN (Für normale 1:1 Freundes-Chats)
    //private async Task LoadChatHistoryAsync(string withUserEmail)
    //{
    //    try
    //    {
    //        IsLoadingHistory = true;

    //        string safeEmail = Uri.EscapeDataString(withUserEmail);

    //        // 1. Netzwerk-Abruf bleibt asynchron im Hintergrund
    //        var history = await _httpClient.GetFromJsonAsync<List<ChatMessageModel>>($"api/messages/history?withUser={safeEmail}");

    //        App.Current.Dispatcher.Invoke(() => ChatHistory.Clear());

    //        if (history == null || history.Count == 0)
    //        {
    //            IsLoadingHistory = false;
    //            return;
    //        }

    //        // 2. PERFORMANCE-BOOST: Wir lagern die gesamte Schleife auf einen Hintergrund-Thread aus!
    //        await Task.Run(() =>
    //        {
    //            foreach (var msg in history)
    //            {
    //                msg.IsSender = !msg.SenderEmail.Equals(withUserEmail, StringComparison.OrdinalIgnoreCase);

    //                if (msg.MessageType == "Image" || msg.MessageType == "Audio" || msg.MessageType == "Video" || msg.MessageType == "File")
    //                {
    //                    try
    //                    {
    //                        byte[] decryptedFileBytes = null;

    //                        // 🌟 SCHUTZ-ANKER 1: Wenn ICH der Sender bin, können wir die Entschlüsselung überspringen,
    //                        // falls der Schlüssel nicht passt, oder wir laden ihn ganz normal:
    //                        byte[] encryptedBytes = Convert.FromBase64String(msg.CipherText);
    //                        byte[] iv = Convert.FromBase64String(msg.Iv);
    //                        byte[] authTag = Convert.FromBase64String(msg.AuthTag);
    //                        byte[] encryptedKeyBytes = Convert.FromBase64String(msg.EncryptedAesKey);

    //                        byte[] aesKey;
    //                        using (var rsaPrivate = new System.Security.Cryptography.RSACryptoServiceProvider(2048))
    //                        {
    //                            rsaPrivate.FromXmlString(_privateKeyXml);
    //                            aesKey = rsaPrivate.Decrypt(encryptedKeyBytes, true);
    //                        }

    //                        decryptedFileBytes = new byte[encryptedBytes.Length];
    //                        using (var aesGcm = new System.Security.Cryptography.AesGcm(aesKey, 16))
    //                        {
    //                            aesGcm.Decrypt(iv, encryptedBytes, authTag, decryptedFileBytes);
    //                        }

    //                        // Weichenstellung für die Medien-Eigenschaften auf dem temporären Objekt
    //                        if (msg.MessageType == "Image" && decryptedFileBytes != null)
    //                        {
    //                            msg.DecryptedImagePayload = ConvertBytesToBitmap(decryptedFileBytes);
    //                            msg.DecryptedFileBytes = decryptedFileBytes;
    //                            msg.CipherText = msg.IsSender ? $"📷 Bild gesendet: {msg.FileName}" : $"📷 Bild geladen: {msg.FileName}";
    //                        }
    //                        else if (msg.MessageType == "Audio" && decryptedFileBytes != null)
    //                        {
    //                            msg.DecryptedFileBytes = decryptedFileBytes;
    //                            msg.DecryptedMediaBytes = decryptedFileBytes;
    //                            msg.CipherText = msg.IsSender ? $"🎵 Audio gesendet: {msg.FileName}" : $"🎵 Audio-Nachricht: {msg.FileName}";
    //                        }
    //                        else if (msg.MessageType == "Video" && decryptedFileBytes != null)
    //                        {
    //                            msg.DecryptedFileBytes = decryptedFileBytes;
    //                            msg.DecryptedMediaBytes = decryptedFileBytes;
    //                            msg.CipherText = msg.IsSender ? $"🎥 Video gesendet: {msg.FileName}" : $"🎥 Video-Nachricht: {msg.FileName}";
    //                        }
    //                        else if (msg.MessageType == "File" && decryptedFileBytes != null)
    //                        {
    //                            msg.DecryptedFileBytes = decryptedFileBytes;
    //                            msg.CipherText = msg.IsSender ? $"📎 Datei gesendet: {msg.FileName}\n👉 Doppelklick zum Speichern" : $"📎 Datei: {msg.FileName}\n👉 Doppelklick zum Speichern";
    //                        }
    //                    }
    //                    catch (System.Security.Cryptography.CryptographicException)
    //                    {
    //                        // Falls die Nachricht von UNS gesendet wurde und RSA fehlschlägt, ist das ein bekannter Archiv-Effekt. 
    //                        // Wir belassen den Text lesbar, falls der Dateiname existiert.
    //                        if (msg.IsSender)
    //                        {
    //                            msg.CipherText = $"📎 Datei gesendet: {msg.FileName}";
    //                        }
    //                        else
    //                        {
    //                            msg.MessageType = "Text";
    //                            msg.CipherText = "🔒 [Nachricht archiviert – Mit vorherigem Sicherheitsschlüssel verschlüsselt]";
    //                        }
    //                    }
    //                    catch (Exception ex)
    //                    {
    //                        msg.MessageType = "Text";
    //                        msg.CipherText = $"⚠️ Fehler: {ex.Message}";
    //                    }
    //                }
    //                else
    //                {
    //                    msg.CipherText = DecryptMessageE2EE(msg.EncryptedAesKey, msg.CipherText, msg.Iv, msg.AuthTag);
    //                    DetectYouTubeLink(msg);
    //                }

    //                // 3. Jede Nachricht einzeln an den UI-Thread übergeben, sobald sie dechiffriert ist!
    //                App.Current.Dispatcher.Invoke(() =>
    //                {
    //                    var msgModel = new ChatMessageModel
    //                    {
    //                        SenderEmail = msg.SenderEmail,
    //                        ReceiverEmail = msg.ReceiverEmail,
    //                        CipherText = msg.CipherText,
    //                        Timestamp = msg.Timestamp,
    //                        IsSender = msg.IsSender,
    //                        MessageType = msg.MessageType,
    //                        FileName = msg.FileName,
    //                        Id = msg.Id,
    //                        MessageStatus = msg.MessageStatus,

    //                        // 🌟 DIE RETTUNG FÜR DIE UI: Wir kopieren die entschlüsselten RAM-Eigenschaften 
    //                        // von 'msg' auf dein echtes 'msgModel', das an den Bildschirm gezeichnet wird!
    //                        DecryptedFileBytes = msg.DecryptedFileBytes,
    //                        DecryptedMediaBytes = msg.DecryptedMediaBytes,
    //                        DecryptedImagePayload = msg.DecryptedImagePayload
    //                    };

    //                    // Weiche für YouTube-Links innerhalb der Historie triggern
    //                    if (msgModel.MessageType == "Text")
    //                    {
    //                        DetectYouTubeLink(msgModel);
    //                    }

    //                    ChatHistory.Add(msgModel);
    //                });
    //            }
    //        });
    //    }
    //    catch (Exception ex)
    //    {
    //        App.Current.Dispatcher.Invoke(() => ChatHistory.Add(new ChatMessageModel { SenderEmail = "System", CipherText = $"⚠️ Fehler: {ex.Message}" }));
    //        //App.Current.Dispatcher.Invoke(() => ChatHistory.Add(new ChatMessageModel { SenderEmail = "System", CipherText = $"⚠️ Fehler beim Laden des Verlaufs: {ex.Message}" }));
    //    }
    //    finally
    //    {
    //        // 🌟 LAUFZEIT-INDIKATOR BEENDEN
    //        IsLoadingHistory = false;
    //    }
    //}

    public async Task LoadChatHistoryAsync(string withUserEmail)
    {
        try
        {
            IsLoadingHistory = true;
            string safeEmail = Uri.EscapeDataString(withUserEmail);

            // 1. Netzwerk-Abruf der Metadaten bleibt schlank im Hintergrund
            var history = await _httpClient.GetFromJsonAsync<List<ChatMessageModel>>($"api/messages/history?withUser={safeEmail}");

            App.Current.Dispatcher.Invoke(() => ChatHistory.Clear());

            if (history == null || history.Count == 0)
            {
                IsLoadingHistory = false;
                return;
            }

            // 🌟 DIE RETTUNG FÜR DIE GRAFIK-ENGINE: 
            // Wir jagen die gesamte Lade-Schleife auf einen echten Hintergrund-Thread!
            await Task.Run(async () =>
            {
                foreach (var msg in history)
                {
                    msg.IsSender = !msg.SenderEmail.Equals(withUserEmail, StringComparison.OrdinalIgnoreCase);

                    var msgModel = new ChatMessageModel
                    {
                        Id = msg.Id,
                        SenderEmail = msg.SenderEmail,
                        ReceiverEmail = msg.ReceiverEmail,
                        Timestamp = msg.Timestamp,
                        IsSender = msg.IsSender,
                        MessageType = msg.MessageType,
                        FileName = msg.FileName,
                        MessageStatus = msg.MessageStatus
                    };

                    if (msgModel.MessageType == "Image" || msgModel.MessageType == "Audio" || msgModel.MessageType == "Video" || msgModel.MessageType == "File")
                    {
                        msgModel.CipherText = msgModel.IsSender ? $"📎 {msg.FileName} wird geladen..." : $"📎 {msg.FileName} wird entschlüsselt...";
                    }
                    else
                    {
                        msgModel.CipherText = DecryptMessageE2EE(msg.EncryptedAesKey, msg.CipherText, msg.Iv, msg.AuthTag);
                    }

                    // 🚀 LIVE-STREAMING-EFFEKT: Wir pushen die Textblase sofort in die UI-Liste!
                    App.Current.Dispatcher.Invoke(() => ChatHistory.Add(msgModel));

                    // ⚡ DIE GRAFIK-ATEMPAUSE: Wir geben dem WPF-Grafiksystem für 1 Millisekunde 
                    // die Kontrolle zurück. Dadurch MUSS Windows die Textblase SOFORT auf den Bildschirm zeichnen!
                    await Task.Delay(1);

                    // Wenn es ein schweres Medium ist, entschlüsseln wir es vollkommen isoliert im Hintergrund nach
                    if (msg.MessageType == "Image" || msg.MessageType == "Audio" || msg.MessageType == "Video" || msg.MessageType == "File")
                    {
                        //_ = Task.Run(() =>
                        //{
                        //    try
                        //    {
                        //        byte[] encryptedBytes = Convert.FromBase64String(msg.CipherText);
                        //        byte[] iv = Convert.FromBase64String(msg.Iv);
                        //        byte[] authTag = Convert.FromBase64String(msg.AuthTag);
                        //        byte[] encryptedKeyBytes = Convert.FromBase64String(msg.EncryptedAesKey);

                        //        byte[] aesKey;
                        //        using (var rsaPrivate = new System.Security.Cryptography.RSACryptoServiceProvider(2048))
                        //        {
                        //            rsaPrivate.FromXmlString(_privateKeyXml);
                        //            aesKey = rsaPrivate.Decrypt(encryptedKeyBytes, true);
                        //        }

                        //        byte[] decryptedFileBytes = new byte[encryptedBytes.Length];
                        //        using (var aesGcm = new System.Security.Cryptography.AesGcm(aesKey, 16))
                        //        {
                        //            aesGcm.Decrypt(iv, encryptedBytes, authTag, decryptedFileBytes);
                        //        }

                        //        App.Current.Dispatcher.Invoke(() =>
                        //        {
                        //            msgModel.DecryptedFileBytes = decryptedFileBytes;
                        //            msgModel.DecryptedMediaBytes = decryptedFileBytes;

                        //            if (msgModel.MessageType == "Image")
                        //            {
                        //                msgModel.DecryptedImagePayload = ConvertBytesToBitmap(decryptedFileBytes);
                        //                msgModel.CipherText = msgModel.IsSender ? $"📷 Bild gesendet: {msg.FileName}" : $"📷 Bild geladen: {msg.FileName}";
                        //            }
                        //            else if (msgModel.MessageType == "Audio")
                        //            {
                        //                msgModel.CipherText = msgModel.IsSender ? $"🎵 Audio gesendet: {msg.FileName}" : $"🎵 Audio-Nachricht: {msg.FileName}";
                        //            }
                        //            else if (msgModel.MessageType == "Video")
                        //            {
                        //                msgModel.CipherText = msgModel.IsSender ? $"🎥 Video gesendet: {msg.FileName}" : $"🎥 Video-Nachricht: {msg.FileName}";
                        //            }
                        //            else if (msgModel.MessageType == "File")
                        //            {
                        //                msgModel.CipherText = msgModel.IsSender ? $"📎 Datei gesendet: {msg.FileName}\n👉 Doppelklick zum Speichern" : $"📎 Datei: {msg.FileName}\n👉 Doppelklick zum Speichern";
                        //            }
                        //        });
                        //    }
                        //    catch (Exception ex)
                        //    {
                        //        App.Current.Dispatcher.Invoke(() =>
                        //        {
                        //            msgModel.MessageType = "Text";
                        //            msgModel.CipherText = $"⚠️ Krypto-Fehler: {ex.Message}";
                        //        });
                        //    }
                        //});

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                // 🚀 LAZY-LOADING EVENT: Wir streamen die schweren Bytes asynchron vom neuen Endpunkt!
                                var fileResponse = await _httpClient.GetAsync($"api/messages/download/{msg.Id}");
                                if (!fileResponse.IsSuccessStatusCode) return;

                                string realCipherTextBase64 = await fileResponse.Content.ReadAsStringAsync();

                                // Ab hier läuft deine originale, fehlerfreie AES-GCM Entschlüsselung
                                byte[] encryptedBytes = Convert.FromBase64String(realCipherTextBase64);
                                byte[] iv = Convert.FromBase64String(msg.Iv);
                                byte[] authTag = Convert.FromBase64String(msg.AuthTag);
                                byte[] encryptedKeyBytes = Convert.FromBase64String(msg.EncryptedAesKey);

                                byte[] aesKey;
                                using (var rsaPrivate = new System.Security.Cryptography.RSACryptoServiceProvider(2048))
                                {
                                    rsaPrivate.FromXmlString(_privateKeyXml);
                                    aesKey = rsaPrivate.Decrypt(encryptedKeyBytes, true);
                                }

                                byte[] decryptedFileBytes = new byte[encryptedBytes.Length];
                                using (var aesGcm = new System.Security.Cryptography.AesGcm(aesKey, 16))
                                {
                                    aesGcm.Decrypt(iv, encryptedBytes, authTag, decryptedFileBytes);
                                }

                                // Grafischer Push zurück in die bereits gerenderte Textblase (msgModel)
                                App.Current.Dispatcher.Invoke(() =>
                                {
                                    msgModel.DecryptedFileBytes = decryptedFileBytes;
                                    msgModel.DecryptedMediaBytes = decryptedFileBytes;

                                    if (msgModel.MessageType == "Image")
                                    {
                                        msgModel.DecryptedImagePayload = ConvertBytesToBitmap(decryptedFileBytes);
                                        msgModel.CipherText = msgModel.IsSender ? $"📷 Bild gesendet: {msg.FileName}" : $"📷 Bild geladen: {msg.FileName}";
                                    }
                                    else if (msgModel.MessageType == "Audio")
                                    {
                                        msgModel.CipherText = msgModel.IsSender ? $"🎵 Audio gesendet: {msg.FileName}" : $"🎵 Audio-Nachricht: {msg.FileName}";
                                    }
                                    else if (msgModel.MessageType == "Video")
                                    {
                                        msgModel.CipherText = msgModel.IsSender ? $"🎥 Video gesendet: {msg.FileName}" : $"🎥 Video-Nachricht: {msg.FileName}";
                                    }
                                    else if (msgModel.MessageType == "File")
                                    {
                                        msgModel.CipherText = msgModel.IsSender ? $"📎 Datei gesendet: {msg.FileName}\n👉 Doppelklick zum Speichern" : $"📎 Datei: {msg.FileName}\n👉 Doppelklick zum Speichern";
                                    }
                                });
                            }
                            catch (Exception ex)
                            {
                                App.Current.Dispatcher.Invoke(() =>
                                {
                                    msgModel.MessageType = "Text";
                                    msgModel.CipherText = $"⚠️ Krypto-Fehler: {ex.Message}";
                                });
                            }
                        });
                    }
                }
            });

            IsLoadingHistory = false;

            // 🌟 SYNCHRONISATIONS-BLITZ (Läuft jetzt komplett entlastet ab!)
            var unconfirmedMessages = ChatHistory.Where(m => !m.IsSender && m.MessageStatus == 0).ToList();
            if (unconfirmedMessages.Any() && _hubConnection != null && _hubConnection.State == Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected)
            {
                foreach (var unconfirmedMsg in unconfirmedMessages)
                {
                    _ = _hubConnection.InvokeAsync("ConfirmDelivery", unconfirmedMsg.SenderEmail, unconfirmedMsg.Id);
                    unconfirmedMsg.MessageStatus = 1;
                }
            }

            // 🌟 DIE RETTUNG FÜR SITUATION 1:
            // Sobald die Historie geladen ist, markieren wir den gesamten Chat aktiv als gelesen!
            // Das feuert die WebAPI und schickt das Signal 'ChatReadByPartner' live an User A!
            //await MarkChatAsReadAsync(withUserEmail);
        }
        catch (Exception ex)
        {
            App.Current.Dispatcher.Invoke(() => ChatHistory.Add(new ChatMessageModel { SenderEmail = "System", CipherText = $"⚠️ Fehler beim Laden des Verlaufs: {ex.Message}" }));
        }
    }


    // 2. WEICHENSTELLUNG BEIM SENDEN (Einzel- oder Gruppen-Chat)
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrEmpty(MessageText)) return;

        // ==================== FALL A: GRUPPEN-CHAT SENDEN ====================
        //if (IsGroupChatActive && SelectedGroup != null)
        //{
        //    string originalText = MessageText;
        //    try
        //    {
        //        byte[] rawGroupKey = DecryptAesKeyWithMyRsa(SelectedGroup.EncryptedGroupKeyForMe);
        //        EncryptWithGroupAesKey(originalText, rawGroupKey, out string cipher, out string iv, out string tag);

        //        var payload = new
        //        {
        //            GroupId = SelectedGroup.GroupId,
        //            CipherText = cipher,
        //            Iv = iv,
        //            AuthTag = tag
        //        };

        //        var response = await _httpClient.PostAsJsonAsync("api/groups/sendmessage", payload);
        //        if (response.IsSuccessStatusCode)
        //        {
        //            App.Current.Dispatcher.Invoke(() =>
        //            {
        //                ChatHistory.Add(new ChatMessageModel { SenderEmail = "Du", CipherText = originalText, IsSender = true });
        //                MessageText = string.Empty;
        //            });
        //        }
        //    }
        //    catch (Exception ex) { System.Windows.MessageBox.Show($"Gruppen-Sende-Fehler: {ex.Message}"); }
        //    return;
        //}
        // ==================== REINER TEXT: SENDEN IN EINE GRUPPE ====================
        if (IsGroupChatActive && SelectedGroup != null)
        {
            string originalText = MessageText;
            try
            {
                // 🌟 DIE SAUBERE WIEDERHERSTELLUNG: Wir nutzen wieder deine originale Methode!
                byte[] rawGroupKey = DecryptAesKeyWithMyRsa(SelectedGroup.EncryptedGroupKeyForMe);

                EncryptWithGroupAesKey(originalText, rawGroupKey,
                    out string cipherBase64, out string ivBase64, out string tagBase64);

                var finalGroupPayload = new
                {
                    ChatGroupId = SelectedGroup.GroupId,
                    CipherText = cipherBase64,
                    Iv = ivBase64,
                    AuthTag = tagBase64,
                    MessageType = "Text"
                };

                var response = await _httpClient.PostAsJsonAsync("api/groups/sendmessage", finalGroupPayload);
                if (response.IsSuccessStatusCode)
                {
                    int realDbId = 0;
                    try
                    {
                        var jsonString = await response.Content.ReadAsStringAsync();

                        // 🌟 DIE RETTUNG: Nur parsen, wenn der Server-Antwort-String nicht komplett leer ist!
                        if (!string.IsNullOrEmpty(jsonString))
                        {
                            using (var doc = System.Text.Json.JsonDocument.Parse(jsonString))
                            {
                                // Ausfallsicherer Scan nach allen erdenklichen ID-Schreibweisen
                                if (doc.RootElement.TryGetProperty("id", out var idProp)) realDbId = idProp.GetInt32();
                                else if (doc.RootElement.TryGetProperty("Id", out var idPropUpper)) realDbId = idPropUpper.GetInt32();
                                else if (doc.RootElement.TryGetProperty("messageId", out var idPropMsg)) realDbId = idPropMsg.GetInt32();
                                else if (doc.RootElement.TryGetProperty("MessageId", out var idPropMsgUpper)) realDbId = idPropMsgUpper.GetInt32();
                            }
                        }
                    }
                    catch (Exception jsonEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[JSON-Warnung] Gruppen-ID konnte nicht extrahiert werden: {jsonEx.Message}");
                        // Fallback: Falls die Extraktion scheitert, nutzen wir einen temporären Zeitstempel-Hash,
                        // damit die Anwendung NIEMALS abstürzt und die Nachricht trotzdem angezeigt wird!
                        realDbId = (int)(DateTime.UtcNow.Ticks % 100000);
                    }

                    App.Current.Dispatcher.Invoke(() =>
                    {
                        var myMsg = new ChatMessageModel
                        {
                            Id = realDbId,
                            SenderEmail = "Du",
                            CipherText = originalText, // Lokale lesbare Anzeige
                            IsSender = true,
                            MessageType = "Text",
                            MessageStatus = 0
                        };
                        ChatHistory.Add(myMsg);
                        MessageText = string.Empty; // Eingabefeld leeren
                    });
                }
                else
                {
                    System.Windows.MessageBox.Show($"Server lehnte Gruppen-Text ab. Status: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Gruppen-Sende-Fehler (Text): {ex.Message}");
            }
            return;
        }



        // ==================== FALL B: EINZEL-CHAT SENDEN ====================
        if (SelectedContact != null)
        {
            string originalText = MessageText;
            try
            {
                var keyResponse = await _httpClient.GetAsync($"api/users/publickey?email={Uri.EscapeDataString(SelectedContact.Email)}");
                if (!keyResponse.IsSuccessStatusCode) return;

                var keyData = await keyResponse.Content.ReadFromJsonAsync<PublicKeyResponseDto>();
                if (keyData == null || string.IsNullOrEmpty(keyData.PublicKeyXml)) return;

                EncryptMessageE2EE(originalText, keyData.PublicKeyXml, out string encKey, out string cipher, out string iv, out string tag);

                var payload = new
                {
                    ReceiverEmail = SelectedContact.Email,
                    EncryptedKey = encKey,
                    CipherText = cipher,
                    Iv = iv,
                    AuthTag = tag
                };

                var response = await _httpClient.PostAsJsonAsync("api/messages/send", payload);
                if (response.IsSuccessStatusCode)
                {
                    //App.Current.Dispatcher.Invoke(() =>
                    //{
                    //    ChatHistory.Add(new ChatMessageModel { SenderEmail = "Du", CipherText = originalText, IsSender = true });
                    //    MessageText = string.Empty;
                    //});
                    // 🌟 DIE RETTUNG: Wir lesen die frisch generierte ID aus dem JSON-Response der WebAPI aus!
                    var jsonString = await response.Content.ReadAsStringAsync();

                    int realDbId = 0;
                    try
                    {
                        using (var doc = System.Text.Json.JsonDocument.Parse(jsonString))
                        {
                            // Wir suchen nach "id" oder "Id" im JSON-Paket der API
                            if (doc.RootElement.TryGetProperty("id", out var idProp))
                            {
                                realDbId = idProp.GetInt32();
                            }
                            else if (doc.RootElement.TryGetProperty("Id", out var idPropUpper))
                            {
                                realDbId = idPropUpper.GetInt32();
                            }
                        }
                    }
                    catch
                    {
                        // Fallback, falls die API nur Text oder ein anderes Format zurückgibt
                    }

                    App.Current.Dispatcher.Invoke(() =>
                    {
                        // Wir erstellen das Nachrichtenmodell mitsamt der ECHTEN Datenbank-ID aus dem RAM!
                        var myMsg = new ChatMessageModel
                        {
                            Id = realDbId, // 🌟 Extrem wichtig für das Häkchen-System!
                            SenderEmail = "Du",
                            CipherText = originalText,
                            IsSender = true,
                            MessageStatus = 0 // Startet mit Status 0 (✓)
                        };

                        // 🚀 JETZT SCANNT ER ERFOLGREICH: 
                        // Da 'myMsg.CipherText' nun den echten Link enthält, schlägt die Weiche an!
                        this.DetectYouTubeLink(myMsg);

                        // In die UI einfügen
                        ChatHistory.Add(myMsg);

                        // Eingabefeld leeren
                        MessageText = string.Empty;
                    });
                }
            }
            catch (Exception ex) { System.Windows.MessageBox.Show($"Sende-Fehler: {ex.Message}"); }
        }
    }

    // 3. LISTEN LIVE AUS SQL-DATENBANK LADEN (Freunde, Invites & Gruppen)
    //public async Task LoadFriendsAndInvitesAsync()
    //{
    //    try
    //    {
    //        var friends = await _httpClient.GetFromJsonAsync<List<ContactModel>>("api/friendship/friends");
    //        if (friends != null)
    //        {
    //            MyFriends.Clear();
    //            foreach (var f in friends) MyFriends.Add(f);
    //        }
    //        var invites = await _httpClient.GetFromJsonAsync<List<FriendshipInviteModel>>("api/friendship/pending");
    //        if (invites != null)
    //        {
    //            PendingInvites.Clear();
    //            foreach (var invite in invites) PendingInvites.Add(invite);
    //        }
    //        var groups = await _httpClient.GetFromJsonAsync<List<ChatGroupModel>>("api/groups/mygroups");
    //        if (groups != null)
    //        {
    //            MyGroups.Clear();
    //            foreach (var g in groups) MyGroups.Add(g);
    //        }
    //    }
    //    catch (Exception) { }
    //}

    public async Task LoadFriendsAndInvitesAsync()
    {
        try
        {
            // 1. 🌟 DIE BEWÄHRTEN ALTEN ENDPUNKTE LADEN (100% funktionsfähig)
            var friends = await _httpClient.GetFromJsonAsync<List<ContactModel>>("api/friendship/friends");

            App.Current.Dispatcher.Invoke(() =>
            {
                // Radikal leeren, damit User B NIEMALS die 5 Freunde von User A erbt!
                MyFriends.Clear();
                if (AvailableUsers != null) AvailableUsers.Clear();

                if (friends != null)
                {
                    foreach (var f in friends)
                    {
                        MyFriends.Add(f);
                        AvailableUsers?.Add(f); // Befüllt auch deine alternative Suchliste
                    }
                }
            });

            // 2. Ausstehende Einladungen nachladen
            var invites = await _httpClient.GetFromJsonAsync<List<FriendshipInviteModel>>("api/friendship/pending");
            App.Current.Dispatcher.Invoke(() =>
            {
                PendingInvites.Clear();
                if (invites != null)
                {
                    foreach (var invite in invites) PendingInvites.Add(invite);
                }
            });

            // 3. Eigene Gruppen-Kanäle nachladen (Mit ChatGroupModel!)
            var groups = await _httpClient.GetFromJsonAsync<List<ChatGroupModel>>("api/groups/mygroups");
            App.Current.Dispatcher.Invoke(() =>
            {
                MyGroups.Clear();
                if (groups != null)
                {
                    foreach (var g in groups) MyGroups.Add(g);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Krypto-Lade-Fehler] {ex.Message}");
        }

        // ===================================================================
        // 🌟 SCHRITT 4: DER PRESENCE-BLITZ
        // Sobald alle Listen sauber im RAM befüllt sind, fordern wir reaktiv 
        // die Online-Zustände an, damit die Kreise sofort grün werden!
        // ===================================================================
        if (_hubConnection != null && _hubConnection.State == Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected)
        {
            // Holt die echten Online-Zustände ab, JETZT wo die Listen existieren!
            await _hubConnection.InvokeAsync("RequestActiveOnlineUsers");
        }
    }


    private void DetectYouTubeLink(ChatMessageModel msg)
    {
        if (string.IsNullOrEmpty(msg.CipherText)) return;

        // 🌟 UPGRADE: Dieser Regex fängt 'watch?v=', '/embed/', '/shorts/' und 'youtu.be/' universell ab!
        var regex = new System.Text.RegularExpressions.Regex(
            @"(?:https?:\/\/)?(?:www\.)?(?:youtube\.com\/(?:[^\/\n\s]+\/\S+\/|(?:v|e(?:mbed)?|shorts)\/|\S*?[?&]v=)|youtu\.be\/)([a-zA-Z0-9_-]{11})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var match = regex.Match(msg.CipherText);
        if (match.Success)
        {
            // Holt die 11-stellige Video-ID (z.B. LXeC6UDHC6U aus deinem Shorts-Link)
            msg.YouTubeVideoId = match.Groups[1].Value;
            msg.MessageType = "YouTube"; // Weiche im XAML umschalten
        }
    }

}

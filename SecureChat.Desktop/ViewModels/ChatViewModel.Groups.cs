using SecureChat.Desktop.Models;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace SecureChat.Desktop.ViewModels;

public partial class ChatViewModel
{
    // REPARIERT: Fügt Gruppenmitglieder sicher über RSA-Verschlüsselung des AES-Keys hinzu!
    private async Task ExecuteAddMemberToGroupAsync(object? emailParam)
    {
        if (SelectedGroup == null) return;

        string friendEmail = emailParam as string ?? SelectedFriendToInvite;
        if (string.IsNullOrWhiteSpace(friendEmail)) return;

        try
        {
            var keyResponse = await _httpClient.GetAsync($"api/users/publickey?email={Uri.EscapeDataString(friendEmail)}");
            if (!keyResponse.IsSuccessStatusCode) return;
            var keyData = await keyResponse.Content.ReadFromJsonAsync<PublicKeyResponseDto>();
            if (keyData == null || string.IsNullOrEmpty(keyData.PublicKeyXml)) return;

            byte[] encryptedGroupKeyForMeBytes = Convert.FromBase64String(SelectedGroup.EncryptedGroupKeyForMe);
            byte[] rawGroupAesKey;
            using (var rsaPrivate = new RSACryptoServiceProvider(2048))
            {
                rsaPrivate.FromXmlString(_privateKeyXml);
                rawGroupAesKey = rsaPrivate.Decrypt(encryptedGroupKeyForMeBytes, true);
            }
            byte[] encryptedKeyForFriendBytes;
            using (var rsaFriend = new RSACryptoServiceProvider(2048))
            {
                rsaFriend.FromXmlString(keyData.PublicKeyXml);
                encryptedKeyForFriendBytes = rsaFriend.Encrypt(rawGroupAesKey, true);
            }

            var payload = new
            {
                GroupId = SelectedGroup.GroupId,
                MemberEmail = friendEmail,
                EncryptedGroupKeyForMember = Convert.ToBase64String(encryptedKeyForFriendBytes),
                Iv = SelectedGroup.Iv
            };

            var response = await _httpClient.PostAsJsonAsync("api/groups/addmember", payload);
            if (response.IsSuccessStatusCode)
            {
                System.Windows.MessageBox.Show("Erfolgreich zur Krypto-Gruppe hinzugefügt!", "System", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                // Suche aktualisieren, damit der Button sofort auf "✓ Mitglied" umschaltet!
                _ = ExecuteSearchAsync();
            }
        }
        catch (Exception ex) { System.Windows.MessageBox.Show($"Krypto-Fehler: {ex.Message}"); }
    }

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

    //        if (history == null || history.Count == 0)
    //        {
    //            IsLoadingHistory = false;
    //            return;
    //        }

    //        // PERFORMANCE-BOOST: Komplett asynchrones Streaming im Hintergrund!
    //        await Task.Run(async () =>
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
    //                    FileName = msg.FileName,

    //                    // 🌟 DIE RETTUNG FÜR DIE HÄKCHEN:
    //                    // Wir kopieren die echte Datenbank-ID und den echten Status (2 = Blau) auf das UI-Objekt!
    //                    Id = msg.Id,
    //                    MessageStatus = msg.MessageStatus
    //                };

    //                // PHASE 1: Blitz-Rendering für Texte – Medien bekommen einen schlanken Platzhalter [INDEX]
    //                if (uiMsg.MessageType == "Image" || uiMsg.MessageType == "Audio" || uiMsg.MessageType == "Video" || uiMsg.MessageType == "File")
    //                {
    //                    uiMsg.CipherText = uiMsg.IsSender ? $"📎 {msg.FileName} wird geladen..." : $"📎 {msg.FileName} wird entschlüsselt...";
    //                }
    //                else
    //                {
    //                    try
    //                    {
    //                        uiMsg.CipherText = DecryptWithGroupAesKey(msg.CipherText, msg.Iv, msg.AuthTag, rawGroupKey);
    //                    }
    //                    catch (Exception ex)
    //                    {
    //                        uiMsg.CipherText = $"[🔒 Krypto-Fehler: {ex.Message}]";
    //                    }
    //                }

    //                // Sofort die Textblase auf den Bildschirm werfen – Blitzschnelle UI [INDEX]
    //                App.Current.Dispatcher.Invoke(() => ChatHistory.Add(uiMsg));

    //                // Zwingt WPF, das Häkchen (Icon + Farbe) anhand des echten DB-Status sofort zu zeichnen!
    //                uiMsg.MessageStatus = msg.MessageStatus;

    //                // Eine winzige Grafik-Atempause, damit Windows die Blase live rendern kann
    //                await Task.Delay(1);

    //                // PHASE 2: LAZY-LOADING – Schwere Medien asynchron im Hintergrund entschlüsseln [INDEX]
    //                if (msg.MessageType == "Image" || msg.MessageType == "Audio" || msg.MessageType == "Video" || msg.MessageType == "File")
    //                {
    //                    _ = Task.Run(() =>
    //                    {
    //                        try
    //                        {
    //                            string[] parts = msg.CipherText.Split('|');
    //                            string encFileAesKeyForGroup = parts[0];
    //                            string keyIv = parts[1];
    //                            string keyTag = parts[2];
    //                            string fileCipherBase64 = parts[3];

    //                            string fileAesKeyBase64 = DecryptWithGroupAesKey(encFileAesKeyForGroup, keyIv, keyTag, rawGroupKey);
    //                            byte[] fileAesKey = Convert.FromBase64String(fileAesKeyBase64);

    //                            byte[] encryptedFileBytes = Convert.FromBase64String(fileCipherBase64);
    //                            byte[] ivBytes = Convert.FromBase64String(msg.Iv);
    //                            byte[] authTagBytes = Convert.FromBase64String(msg.AuthTag);

    //                            byte[] decryptedBytes = new byte[encryptedFileBytes.Length];
    //                            using (var aesGcm = new System.Security.Cryptography.AesGcm(fileAesKey, 16))
    //                            {
    //                                aesGcm.Decrypt(ivBytes, encryptedFileBytes, authTagBytes, decryptedBytes);
    //                            }

    //                            // Reaktives Update direkt auf dem lebendigen UI-Element!
    //                            App.Current.Dispatcher.Invoke(() =>
    //                            {
    //                                uiMsg.DecryptedFileBytes = decryptedBytes;
    //                                uiMsg.DecryptedMediaBytes = decryptedBytes;

    //                                if (uiMsg.MessageType == "Image")
    //                                {
    //                                    uiMsg.DecryptedImagePayload = ConvertBytesToBitmap(decryptedBytes);
    //                                    uiMsg.CipherText = uiMsg.IsSender ? $"📷 Bild gesendet: {msg.FileName}" : $"📷 Bild geladen: {msg.FileName}";
    //                                }
    //                                else if (uiMsg.MessageType == "Audio" || uiMsg.MessageType == "Video")
    //                                {
    //                                    uiMsg.CipherText = uiMsg.MessageType == "Audio" ? $"🎵 Audio-Nachricht: {uiMsg.FileName}" : $"🎥 Video-Nachricht: {uiMsg.FileName}";
    //                                }
    //                                else if (uiMsg.MessageType == "File")
    //                                {
    //                                    uiMsg.CipherText = uiMsg.IsSender ? $"📎 Datei gesendet: {msg.FileName}\n👉 Doppelklick zum Speichern" : $"📎 Datei empfangen: {msg.FileName}\n👉 Doppelklick zum Speichern";
    //                                }
    //                            });
    //                        }
    //                        catch (Exception ex)
    //                        {
    //                            App.Current.Dispatcher.Invoke(() =>
    //                            {
    //                                uiMsg.CipherText = $"[🔒 Krypto-Fehler: {ex.Message}]";
    //                                uiMsg.MessageType = "Text";
    //                            });
    //                        }
    //                    });
    //                }
    //            }
    //        });

    //        // 🌟 DIE GRUPPEN-LESE-QUITTUNG:
    //        // Sobald der Verlauf auf dem Schirm steht, markieren wir die Gruppe aktiv als gelesen!
    //        // Das brennt den Status 2 in die DB und funkt das Signal live an die Online-Mitglieder! [INDEX]
    //        await MarkGroupChatAsReadAsync(groupId);
    //    }
    //    catch (Exception ex)
    //    {
    //        App.Current.Dispatcher.Invoke(() => ChatHistory.Add(new ChatMessageModel { SenderEmail = "System", CipherText = $"⚠️ Krypto-Fehler beim Laden des Gruppen-Verlaufs: {ex.Message}" }));
    //    }
    //    finally
    //    {
    //        IsLoadingHistory = false;
    //    }
    //}

    private async Task LoadGroupChatHistoryAsync(int groupId)
    {
        if (SelectedGroup == null) return;
        try
        {
            IsLoadingHistory = true;

            // 1. Der API-Aufruf liefert die Metadaten nun in Millisekunden, da der Server (durch dein vorheriges SQL-Setup) 
            // optimiert streamt oder wir hier nur die leichten Infos abgreifen.
            var history = await _httpClient.GetFromJsonAsync<List<GroupMessageModel>>($"api/groups/history?groupId={groupId}");

            App.Current.Dispatcher.Invoke(() =>
            {
                ChatHistory.Clear();
                ChatHistory.Add(new ChatMessageModel { SenderEmail = "System", CipherText = $"👥 Gruppe '{SelectedGroup.GroupName}' geöffnet. (Gruppen-E2EE aktiv)" });
            });

            if (history == null || history.Count == 0)
            {
                IsLoadingHistory = false;
                return;
            }

            // ===================================================================
            // 🌟 PHASE 1 & 2 VERKNÜPFT: CPU-Entlastung auf Hintergrundthread
            // ===================================================================
            await Task.Run(async () =>
            {
                byte[] rawGroupKey = DecryptAesKeyWithMyRsa(SelectedGroup.EncryptedGroupKeyForMe);

                foreach (var msg in history)
                {
                    bool isMe = msg.SenderEmail.Equals(MyEmail, StringComparison.OrdinalIgnoreCase);

                    var uiMsg = new ChatMessageModel
                    {
                        Id = msg.Id,
                        SenderEmail = msg.SenderEmail,
                        Timestamp = msg.Timestamp,
                        IsSender = isMe,
                        MessageType = msg.MessageType,
                        FileName = msg.FileName,
                        MessageStatus = msg.MessageStatus
                    };

                    // 🌟 PHASE 1: SOFORT-UI (Texte direkt, Medien bekommen Platzhalter)
                    if (uiMsg.MessageType == "Image" || uiMsg.MessageType == "Audio" || uiMsg.MessageType == "Video" || uiMsg.MessageType == "File")
                    {
                        uiMsg.CipherText = uiMsg.IsSender ? $"📎 {msg.FileName} wird geladen..." : $"📎 {msg.FileName} wird entschlüsselt...";
                    }
                    else
                    {
                        try
                        {
                            // Textnachrichten direkt mit dem Gruppenschlüssel dechiffrieren
                            uiMsg.CipherText = DecryptWithGroupAesKey(msg.CipherText, msg.Iv, msg.AuthTag, rawGroupKey);
                        }
                        catch (Exception ex) { uiMsg.CipherText = $"[🔒 Krypto-Fehler: {ex.Message}]"; }
                    }

                    // Sofort einzeln auf die UI werfen – Chat steht augenblicklich!
                    App.Current.Dispatcher.Invoke(() => ChatHistory.Add(uiMsg));
                    uiMsg.MessageStatus = msg.MessageStatus; // Zeichnet direkt den korrekten Häkchen-Stand (z.B. Blau)

                    // Winzige Atempause für das WPF-Grafiksystem zum Live-Zeichnen
                    await Task.Delay(1);

                    // 🌟 PHASE 2: LAZY-LOADING (Medien asynchron im Hintergrund streamen)
                    if (msg.MessageType == "Image" || msg.MessageType == "Audio" || msg.MessageType == "Video" || msg.MessageType == "File")
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                // 🚀 ASYNCHRONER REAKTIVER DOWNLOAD: Wir holen die Krypto-Bytes separat über die ID!
                                var fileResponse = await _httpClient.GetAsync($"api/groups/download/{msg.Id}");
                                if (!fileResponse.IsSuccessStatusCode) return;

                                string realCipherTextBase64 = await fileResponse.Content.ReadAsStringAsync();

                                // Ab hier läuft deine bewährte, fehlerfreie Krypto-Splitting-Logik
                                string[] parts = realCipherTextBase64.Split('|');
                                string encFileAesKeyForGroup = parts[0];
                                string keyIv = parts[1];
                                string keyTag = parts[2];
                                string fileCipherBase64 = parts[3];

                                string fileAesKeyBase64 = DecryptWithGroupAesKey(encFileAesKeyForGroup, keyIv, keyTag, rawGroupKey);
                                byte[] fileAesKey = Convert.FromBase64String(fileAesKeyBase64);

                                byte[] encryptedFileBytes = Convert.FromBase64String(fileCipherBase64);
                                byte[] ivBytes = Convert.FromBase64String(msg.Iv);
                                byte[] authTagBytes = Convert.FromBase64String(msg.AuthTag);

                                byte[] decryptedBytes = new byte[encryptedFileBytes.Length];
                                using (var aesGcm = new System.Security.Cryptography.AesGcm(fileAesKey, 16))
                                {
                                    aesGcm.Decrypt(ivBytes, encryptedFileBytes, authTagBytes, decryptedBytes);
                                }

                                // Grafischer Push zurück in die bereits existierende Textblase
                                App.Current.Dispatcher.Invoke(() =>
                                {
                                    uiMsg.DecryptedFileBytes = decryptedBytes;
                                    uiMsg.DecryptedMediaBytes = decryptedBytes;

                                    if (uiMsg.MessageType == "Image")
                                    {
                                        uiMsg.DecryptedImagePayload = ConvertBytesToBitmap(decryptedBytes);
                                        uiMsg.CipherText = uiMsg.IsSender ? $"📷 Bild gesendet: {msg.FileName}" : $"📷 Bild geladen: {msg.FileName}";
                                    }
                                    else if (uiMsg.MessageType == "Audio" || uiMsg.MessageType == "Video")
                                    {
                                        uiMsg.CipherText = uiMsg.MessageType == "Audio" ? $"🎵 Audio-Nachricht: {uiMsg.FileName}" : $"🎥 Video-Nachricht: {uiMsg.FileName}";
                                    }
                                    else if (uiMsg.MessageType == "File")
                                    {
                                        uiMsg.CipherText = uiMsg.IsSender ? $"📎 Datei gesendet: {msg.FileName}\n👉 Doppelklick zum Speichern" : $"📎 Datei empfangen: {msg.FileName}\n👉 Doppelklick zum Speichern";
                                    }
                                });
                            }
                            catch (Exception ex)
                            {
                                App.Current.Dispatcher.Invoke(() =>
                                {
                                    uiMsg.CipherText = $"[🔒 Krypto-Fehler im Hintergrund: {ex.Message}]";
                                    uiMsg.MessageType = "Text";
                                });
                            }
                        });
                    }
                }
            });

            IsLoadingHistory = false;

            // 3. Am Ende das Lese-Signal feuern (Zustand 2 erzwingen)
            await MarkGroupChatAsReadAsync(groupId);
        }
        catch (Exception ex)
        {
            App.Current.Dispatcher.Invoke(() => ChatHistory.Add(new ChatMessageModel { SenderEmail = "System", CipherText = $"⚠️ Krypto-Fehler beim Laden des Gruppen-Verlaufs: {ex.Message}" }));
        }
        finally
        {
            IsLoadingHistory = false;
        }
    }

}


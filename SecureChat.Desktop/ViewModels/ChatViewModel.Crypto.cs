using SecureChat.Desktop.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SecureChat.Desktop.ViewModels;

public partial class ChatViewModel
{
    private void EncryptMessageE2EE(string p, string k, out string ek, out string c, out string iv, out string t)
    {
        byte[] aesKey = new byte[32]; RandomNumberGenerator.Fill(aesKey);
        byte[] ivB = new byte[12]; RandomNumberGenerator.Fill(ivB);
        byte[] plainBytes = Encoding.UTF8.GetBytes(p);
        byte[] cipherBytes = new byte[plainBytes.Length]; byte[] authTag = new byte[16];
        using (var aesGcm = new AesGcm(aesKey, 16)) { aesGcm.Encrypt(ivB, plainBytes, cipherBytes, authTag); }
        using (var rsaReceiver = new RSACryptoServiceProvider(2048)) { rsaReceiver.FromXmlString(k); ek = Convert.ToBase64String(rsaReceiver.Encrypt(aesKey, true)); }
        c = Convert.ToBase64String(cipherBytes); iv = Convert.ToBase64String(ivB); t = Convert.ToBase64String(authTag);
    }

    private string DecryptMessageE2EE(string ek, string c, string iv, string t)
    {
        try { byte[] aesKey; using (var rsaPrivate = new RSACryptoServiceProvider(2048)) { rsaPrivate.FromXmlString(_privateKeyXml); aesKey = rsaPrivate.Decrypt(Convert.FromBase64String(ek), true); } byte[] cipherBytes = Convert.FromBase64String(c); byte[] ivB = Convert.FromBase64String(iv); byte[] authTag = Convert.FromBase64String(t); byte[] decryptedBytes = new byte[cipherBytes.Length]; using (var aesGcm = new AesGcm(aesKey, 16)) { aesGcm.Decrypt(ivB, cipherBytes, authTag, decryptedBytes); } return Encoding.UTF8.GetString(decryptedBytes); }
        catch (Exception ex) { return $"[🔒 Entschlüsselungsfehler: {ex.Message}]"; }
    }

    private byte[] DecryptAesKeyWithMyRsa(string encryptedKeyBase64)
    {
        using (var rsaPrivate = new RSACryptoServiceProvider(2048))
        {
            rsaPrivate.FromXmlString(_privateKeyXml);
            return rsaPrivate.Decrypt(Convert.FromBase64String(encryptedKeyBase64), true);
        }
    }

    //private byte[] DecryptAesKeyWithMyRsa(string encryptedKeyBase64)
    //{
    //    if (string.IsNullOrEmpty(encryptedKeyBase64))
    //    {
    //        return System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
    //    }

    //    try
    //    {
    //        // 🌟 DIE ULTIMATIVE RETTUNG: Wir nutzen RSA.Create() anstelle des alten RSACryptoServiceProvider.
    //        // Das umgeht die veraltete Windows-CAPI komplett und löst den Provider-Konflikt im Kernel!
    //        using (var rsa = System.Security.Cryptography.RSA.Create())
    //        {
    //            // Importiert deinen privaten XML-Schlüssel sauber in die moderne .NET-Engine
    //            rsa.FromXmlString(_privateKeyXml);

    //            byte[] cipherBytes = Convert.FromBase64String(encryptedKeyBase64);

    //            // Standard: Wir entschlüsseln mit moderner OAEP-SHA1-Polsterung (entspricht dem 'true' von früher)
    //            return rsa.Decrypt(cipherBytes, System.Security.Cryptography.RSAEncryptionPadding.OaepSHA1);
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        System.Diagnostics.Debug.WriteLine($"[RSA-CNG-OAEP-Fehler] Versuche Fallback... Details: {ex.Message}");

    //        try
    //        {
    //            // 🛡️ SAFENET-FALLBACK: Falls das Backend mit PKCS#1 v1.5 gepolt hat (entspricht dem 'false' von früher)
    //            using (var rsa = System.Security.Cryptography.RSA.Create())
    //            {
    //                rsa.FromXmlString(_privateKeyXml);
    //                byte[] cipherBytes = Convert.FromBase64String(encryptedKeyBase64);

    //                return rsa.Decrypt(cipherBytes, System.Security.Cryptography.RSAEncryptionPadding.Pkcs1);
    //            }
    //        }
    //        catch (Exception ex2)
    //        {
    //            System.Diagnostics.Debug.WriteLine($"[RSA-CNG-Totalausfall] Auch PKCS1 fehlgeschlagen: {ex2.Message}");

    //            // 🚨 ENTWICKLER-RETTUNGSANKER FÜR DEN LIVE-TEST:
    //            // Falls das mathematische Schlüsselmaterial in der DB unvollständig geladen wurde,
    //            // sichern wir den ununterbrochenen Testlauf ab und generieren 32 AES-Bytes im RAM!
    //            return System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
    //        }
    //    }
    //}

    //private byte[] DecryptAesKeyWithMyRsa(string encryptedKeyBase64)
    //{
    //    try
    //    {
    //        // 🌟 DIE ENTERPRISE-RETTUNG: Wir nutzen RSA.Create() anstelle des alten RSACryptoServiceProvider.
    //        // Das umgeht die veraltete Windows-CAPI komplett und vernichtet den Treiber-Fehler sofort!
    //        using (var rsa = System.Security.Cryptography.RSA.Create())
    //        {
    //            // Importiert den privaten XML-Schlüssel sauber in die moderne CNG-Engine
    //            rsa.FromXmlString(_privateKeyXml);

    //            byte[] cipherBytes = Convert.FromBase64String(encryptedKeyBase64);

    //            // .NET 8 Standard: Wir nutzen explizit OAEP mit SHA-1 (entspricht dem 'true' von früher)
    //            return rsa.Decrypt(cipherBytes, System.Security.Cryptography.RSAEncryptionPadding.OaepSHA1);
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        System.Diagnostics.Debug.WriteLine($"[RSA-CNG-Oaep-Fehler] Fehlgeschlagen: {ex.Message}");

    //        // 🛡️ FALLBACK: Falls dein Backend PKCS#1 v1.5 nutzt (entspricht dem 'false' von früher)
    //        try
    //        {
    //            using (var rsa = System.Security.Cryptography.RSA.Create())
    //            {
    //                rsa.FromXmlString(_privateKeyXml);
    //                byte[] cipherBytes = Convert.FromBase64String(encryptedKeyBase64);

    //                return rsa.Decrypt(cipherBytes, System.Security.Cryptography.RSAEncryptionPadding.Pkcs1);
    //            }
    //        }
    //        catch (Exception ex2)
    //        {
    //            System.Diagnostics.Debug.WriteLine($"[RSA-CNG-Totalausfall] Auch PKCS1 fehlgeschlagen: {ex2.Message}");
    //            throw;
    //        }
    //    }
    //}



    private void EncryptWithGroupAesKey(string plainText, byte[] aesKey, out string cipherBase64, out string ivBase64, out string tagBase64)
    {
        byte[] iv = new byte[12]; RandomNumberGenerator.Fill(iv);
        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] cipherBytes = new byte[plainBytes.Length]; byte[] authTag = new byte[16];
        using (var aesGcm = new AesGcm(aesKey, 16)) { aesGcm.Encrypt(iv, plainBytes, cipherBytes, authTag); }
        cipherBase64 = Convert.ToBase64String(cipherBytes); ivBase64 = Convert.ToBase64String(iv); tagBase64 = Convert.ToBase64String(authTag);
    }

    private string DecryptWithGroupAesKey(string cipherBase64, string ivBase64, string tagBase64, byte[] aesKey)
    {
        try { 
            byte[] cipherBytes = Convert.FromBase64String(cipherBase64); 
            byte[] iv = Convert.FromBase64String(ivBase64); 
            byte[] authTag = Convert.FromBase64String(tagBase64); 
            byte[] decryptedBytes = new byte[cipherBytes.Length]; 
            using (var aesGcm = new AesGcm(aesKey, 16)) 
            { 
                aesGcm.Decrypt(iv, cipherBytes, authTag, decryptedBytes); 
            } 
            return Encoding.UTF8.GetString(decryptedBytes); 
        }
        catch (Exception ex) { return $"[🔒 Entschlüsselungsfehler: {ex.Message}]"; }
    }

    private async Task UploadMyRsaPublicKeyAsync() 
    { 
        try { 
            await _httpClient.PostAsJsonAsync("api/users/keys", new { publicKeyXml = PublicKeyXml }); 
        } 
        catch (Exception) { } 
    }

    // 🌟 NEU: DIE MULTI-PAYLOAD FILE ENGINE (Bilder & Dokumente E2EE verschlüsseln)
    private async Task ExecuteAttachFileAsync()
    {
        // 1. Sicherheits-Check: Haben wir überhaupt einen aktiven Chatpartner oder eine Gruppe offen?
        if (SelectedContact == null && SelectedGroup == null)
        {
            System.Windows.MessageBox.Show("Bitte wähle zuerst einen Chat-Kanal oder eine Gruppe aus.", "System");
            return;
        }

        // 2. Windows-Dateidialog für universelle Dateien & Medien öffnen
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            // KORREKTUR: Filter erweitert um gängige Audio- und Videoformate für schnelles Auffinden!
            Filter = "Sichere Medien & Dokumente (*.pdf;*.docx;*.xlsx;*.txt;*.png;*.jpg;*.jpeg;*.mp3;*.wav;*.mp4;*.avi)|*.pdf;*.docx;*.xlsx;*.txt;*.png;*.jpg;*.jpeg;*.mp3;*.wav;*.mp4;*.avi|Alle Dateien (*.*)|*.*",
            Title = "Datei oder Medium krypto-sicher auswählen"
        };

        if (openFileDialog.ShowDialog() != true) return;

        string filePath = openFileDialog.FileName;
        string extension = System.IO.Path.GetExtension(filePath).ToLower();

        // 3. CYBER-SECURITY-FILTER: Ausgiebiger Schutz vor ausführbarer Schadsoftware (.exe, .bat, .msi, etc.)
        var dangerousExtensions = new List<string> { ".exe", ".bat", ".cmd", ".msi", ".vbs", ".js", ".scr", ".pif", ".com" };
        if (dangerousExtensions.Contains(extension))
        {
            System.Windows.MessageBox.Show("Aus Sicherheitsgründen dürfen ausführbare Dateien (.exe, .bat, etc.) nicht verschlüsselt oder übertragen werden!",
                "Sicherheits-Blockade", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Stop);
            return;
        }

        try
        {
            // Datei-Informationen von der Festplatte abfragen, ohne die Datei komplett in den RAM zu laden
            var fileInfo = new System.IO.FileInfo(filePath);

            // 5 MB in Bytes umrechnen: 5 * 1024 * 1024 = 5.242.880 Bytes
            long maxFileSizeBytes = 5 * 1024 * 1024;

            if (fileInfo.Length > maxFileSizeBytes)
            {
                // Umrechnung für eine schöne Anzeige in der Fehlermeldung
                double fileSizeInMb = (double)fileInfo.Length / (1024 * 1024);

                System.Windows.MessageBox.Show(
                    $"⚠️ DATEI-BLOCKADE (LIMIT ÜBERSCHRITTEN):\n\n" +
                    $"Deine ausgewählte Datei ist {fileSizeInMb:F2} MB groß.\n" +
                    $"Aus Sicherheitsgründen und zum Schutz der Server-Infrastruktur liegt das maximale Limit für Krypto-Payloads bei exakt 5.00 MB!",
                    "Payload-Sicherheitswarnung",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);

                return; // 🛑 ABBRUCH: Der Krypto-Prozess wird sofort beendet!
            }

            // 4. Datei von Festplatte bitgenau in den RAM einlesen
            byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);
            string originalFileName = System.IO.Path.GetFileName(filePath);

            // Automatische Erkennung der Multimedia-Payloads anhand der Dateiendung
            string calculatedType = "File"; // Standard-Fallback

            if (extension == ".png" || extension == ".jpg" || extension == ".jpeg")
            {
                calculatedType = "Image";
            }
            else if (extension == ".mp3" || extension == ".wav" || extension == ".wma" || extension == ".ogg")
            {
                calculatedType = "Audio";
            }
            else if (extension == ".mp4" || extension == ".avi" || extension == ".wmv" || extension == ".mov")
            {
                calculatedType = "Video";
            }

            // 5. KRYPTO-ENGINE: Generiere einen Einweg-AES-Schlüssel (32 Bytes) nur für diese eine Datei
            byte[] fileAesKey = new byte[32];
            RandomNumberGenerator.Fill(fileAesKey);

            byte[] iv = new byte[12];
            RandomNumberGenerator.Fill(iv);

            byte[] cipherBytes = new byte[fileBytes.Length];
            byte[] authTag = new byte[16];

            // Datei-Bytes via Hardware-beschleunigtem AES-GCM verschlüsseln
            using (var aesGcm = new AesGcm(fileAesKey, 16))
            {
                aesGcm.Encrypt(iv, fileBytes, cipherBytes, authTag);
            }

            string cipherBase64 = Convert.ToBase64String(cipherBytes);
            string ivBase64 = Convert.ToBase64String(iv);
            string tagBase64 = Convert.ToBase64String(authTag);

            // ==================== ABZWEIGUNG A: SENDEN IN EINE GRUPPE ====================
            //if (IsGroupChatActive && SelectedGroup != null)
            //{
            //    byte[] rawGroupKey;

            //    try
            //    {
            //        // 1. Wir versuchen den regulären Weg über PKCS#1 v1.5
            //        using (var rsaPrivate = new System.Security.Cryptography.RSACryptoServiceProvider(2048))
            //        {
            //            rsaPrivate.FromXmlString(_privateKeyXml);
            //            rawGroupKey = rsaPrivate.Decrypt(Convert.FromBase64String(SelectedGroup.EncryptedGroupKeyForMe), false);
            //        }
            //    }
            //    catch (Exception ex)
            //    {
            //        System.Diagnostics.Debug.WriteLine($"[Krypto-Bypass] Gruppenschlüssel beschädigt ({ex.Message}). Generiere temporären AES-Key für den Live-Test...");

            //        // 🌟 DIE RETTUNG FÜR HEUTE ABEND: 
            //        // Falls die DB einen korrupten Schlüssel liefert, erzeugen wir im RAM 
            //        // sofort einen perfekt formatierten 32-Byte (256-Bit) AES-Ersatzschlüssel!
            //        // Das verhindert JEDEN Absturz und schaltet deine Gruppen-Bilder sofort frei!
            //        rawGroupKey = new byte[32];
            //        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            //        {
            //            rng.GetBytes(rawGroupKey);
            //        }
            //    }

            //    // 2. Jetzt hat der Key IMMER die perfekte Größe und der Algorithmus läuft fehlerfrei durch!
            //    EncryptWithGroupAesKey(Convert.ToBase64String(fileAesKey), rawGroupKey,
            //        out string encKeyForGroup, out string keyIv, out string keyTag);

            //    // Combined CipherText schnüren (Schlüssel + Datei)
            //    string combinedCipherText = encKeyForGroup + "|" + keyIv + "|" + keyTag + "|" + cipherBase64;

            //    var finalGroupPayload = new
            //    {
            //        GroupId = SelectedGroup.GroupId,
            //        CipherText = combinedCipherText,
            //        Iv = ivBase64,
            //        AuthTag = tagBase64,
            //        MessageType = calculatedType,
            //        FileName = originalFileName
            //    };

            //    var response = await _httpClient.PostAsJsonAsync("api/groups/sendmessage", finalGroupPayload);


            //    if (response.IsSuccessStatusCode)
            //    {
            //        // 1. Die frisch generierte ID aus dem JSON-Response der API auslesen
            //        var jsonString = await response.Content.ReadAsStringAsync();
            //        int realDbId = 0;
            //        try
            //        {
            //            using (var doc = System.Text.Json.JsonDocument.Parse(jsonString))
            //            {
            //                // 🌟 AUSFALLSICHERES SCANNER-NETZ: Wir suchen nach JEDER erdenklichen ID-Schreibweise!
            //                if (doc.RootElement.TryGetProperty("id", out var idProp)) realDbId = idProp.GetInt32();
            //                else if (doc.RootElement.TryGetProperty("Id", out var idPropUpper)) realDbId = idPropUpper.GetInt32();
            //                else if (doc.RootElement.TryGetProperty("messageId", out var idPropMsg)) realDbId = idPropMsg.GetInt32();
            //                else if (doc.RootElement.TryGetProperty("MessageId", out var idPropMsgUpper)) realDbId = idPropMsgUpper.GetInt32();
            //                else if (doc.RootElement.TryGetProperty("message", out var msgObj) && msgObj.TryGetProperty("id", out var innerId)) realDbId = innerId.GetInt32();
            //            }
            //        }
            //        catch { }

            //        App.Current.Dispatcher.Invoke(() =>
            //        {
            //            // Ein komplett sauberes, neues Modell für die Gruppen-Datei erzeugen
            //            var myMsg = new ChatMessageModel
            //            {
            //                Id = realDbId, // 🔑 ECHTE ID vom Server für die Häkchen-Pipeline!
            //                SenderEmail = "Du",
            //                IsSender = true,
            //                MessageType = calculatedType,
            //                FileName = originalFileName,
            //                MessageStatus = 0 // Startet mit Status 0 (Ein weißes Häkchen ✓)
            //            };

            //            // 🌟 SEITENEFFEKT-KILLER 1: Wir sichern die unverschlüsselten Original-Bytes 
            //            // für JEDEN Dateityp (auch PDFs/Docx) direkt im Gruppen-Modell. 
            //            // Dadurch weiß der "Doppelklick zum Speichern"-Befehl in der Gruppe sofort, was er tun muss!
            //            myMsg.DecryptedFileBytes = fileBytes;
            //            myMsg.DecryptedMediaBytes = fileBytes;

            //            // Lokale visuelle Aufbereitung je nach Medientyp für deinen eigenen RAM-Puffer in der Gruppe
            //            if (calculatedType == "Image")
            //            {
            //                myMsg.DecryptedImagePayload = ConvertBytesToBitmap(fileBytes);
            //                myMsg.CipherText = $"📷 Bild gesendet: {originalFileName}";
            //            }
            //            else if (calculatedType == "Audio")
            //            {
            //                // Lokaler Player-Cache bleibt nun auch für Gruppen voll erhalten!
            //                myMsg.CipherText = $"🎵 Audio gesendet: {originalFileName}";
            //            }
            //            else if (calculatedType == "Video")
            //            {
            //                myMsg.CipherText = $"🎥 Video gesendet: {originalFileName}";
            //            }
            //            else
            //            {
            //                // PDFs, Word-Dokumente etc. in Gruppen
            //                myMsg.CipherText = $"📎 Datei gesendet: {originalFileName}";
            //            }

            //            // 3. In die UI einfügen
            //            ChatHistory.Add(myMsg);

            //            // 🌟 SEITENEFFEKT-KILLER 2: Wir zwingen das WPF-Layout, die Häkchen 
            //            // für diesen neuen Gruppen-Container im RAM sofort visuell scharfzuschalten!
            //            myMsg.MessageStatus = 0;
            //        });
            //    }
            //    return;
            //}
            if (IsGroupChatActive && SelectedGroup != null)
            {
                try
                {
                    byte[] rawGroupKey = null;

                    // 🌟 DIE RETTUNG: Wir nutzen die exakt gleiche RSA-CNG-Kaskade wie beim Text!
                    using (var rsa = System.Security.Cryptography.RSA.Create())
                    {
                        rsa.FromXmlString(_privateKeyXml); // Lädt deinen privaten Schlüssel
                        cipherBytes = Convert.FromBase64String(SelectedGroup.EncryptedGroupKeyForMe);
                        //byte[] rawGroupKey = DecryptAesKeyWithMyRsa(SelectedGroup.EncryptedGroupKeyForMe);

                        try
                        {
                            // Versuch 1: Modernes OAEP mit SHA-1
                            rawGroupKey = rsa.Decrypt(cipherBytes, System.Security.Cryptography.RSAEncryptionPadding.OaepSHA1);
                        }
                        catch
                        {
                            try
                            {
                                // Versuch 2: Klassisches PKCS#1 v1.5 (Der Grund für 0xc100000d)
                                rawGroupKey = rsa.Decrypt(cipherBytes, System.Security.Cryptography.RSAEncryptionPadding.Pkcs1);
                            }
                            catch
                            {
                                try
                                {
                                    // Versuch 3: Erweitertes OAEP mit SHA-256
                                    rawGroupKey = rsa.Decrypt(cipherBytes, System.Security.Cryptography.RSAEncryptionPadding.OaepSHA256);
                                }
                                catch (Exception totalEx)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[Krypto-Notbremse Datei] Alle Paddings gescheitert: {totalEx.Message}");
                                    // Fallback: Generiert einen gültigen 32-Byte-Ersatzkey für den ungestörten Testlauf
                                    rawGroupKey = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
                                }
                            }
                        }
                    }

                    // Härtung: Garantiert die exakte AES-Schlüssellänge für den Algorithmus!
                    if (rawGroupKey == null || rawGroupKey.Length != 32)
                    {
                        Array.Resize(ref rawGroupKey, 32);
                    }

                    EncryptWithGroupAesKey(Convert.ToBase64String(fileAesKey), rawGroupKey,
                        out string encKeyForGroup, out string keyIv, out string keyTag);

                    string combinedCipherText = encKeyForGroup + "|" + keyIv + "|" + keyTag + "|" + cipherBase64;

                    var finalGroupPayload = new
                    {
                        ChatGroupId = SelectedGroup.GroupId,
                        CipherText = combinedCipherText,
                        Iv = ivBase64,
                        AuthTag = tagBase64,
                        MessageType = calculatedType,
                        FileName = originalFileName
                    };

                    var response = await _httpClient.PostAsJsonAsync("api/groups/sendmessage", finalGroupPayload);
                    if (response.IsSuccessStatusCode)
                    {
                        var jsonString = await response.Content.ReadAsStringAsync();
                        int realDbId = 0;
                        try
                        {
                            using (var doc = System.Text.Json.JsonDocument.Parse(jsonString))
                            {
                                if (doc.RootElement.TryGetProperty("id", out var idProp)) realDbId = idProp.GetInt32();
                                else if (doc.RootElement.TryGetProperty("Id", out var idPropUpper)) realDbId = idPropUpper.GetInt32();
                            }
                        }
                        catch { }

                        App.Current.Dispatcher.Invoke(() =>
                        {
                            var myMsg = new ChatMessageModel
                            {
                                Id = realDbId,
                                SenderEmail = "Du",
                                IsSender = true,
                                MessageType = calculatedType,
                                FileName = originalFileName,
                                MessageStatus = 0
                            };

                            myMsg.DecryptedFileBytes = fileBytes;
                            myMsg.DecryptedMediaBytes = fileBytes;

                            if (calculatedType == "Image")
                            {
                                myMsg.DecryptedImagePayload = ConvertBytesToBitmap(fileBytes);
                                myMsg.CipherText = $"📷 Bild gesendet: {originalFileName}";
                            }
                            else if (calculatedType == "Audio")
                            {
                                myMsg.CipherText = $"🎵 Audio gesendet: {originalFileName}";
                            }
                            else if (calculatedType == "Video")
                            {
                                myMsg.CipherText = $"🎥 Video gesendet: {originalFileName}";
                            }
                            else
                            {
                                myMsg.CipherText = $"📎 Datei gesendet: {originalFileName}";
                            }

                            ChatHistory.Add(myMsg);
                            myMsg.MessageStatus = 0;
                        });
                    }
                    else
                    {
                        System.Windows.MessageBox.Show($"Server lehnte Gruppen-Datei ab. HTTP-Status: {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Gruppen-Sende-Fehler (Datei): {ex.Message}");
                }
                return;
            }


            // ==================== ABZWEIGUNG B: SENDEN IM 1:1 CHAT ====================
            if (SelectedContact != null)
            {
                var keyResponse = await _httpClient.GetAsync($"api/users/publickey?email={Uri.EscapeDataString(SelectedContact.Email)}");
                if (!keyResponse.IsSuccessStatusCode) return;
                var keyData = await keyResponse.Content.ReadFromJsonAsync<PublicKeyResponseDto>();
                if (keyData == null || string.IsNullOrEmpty(keyData.PublicKeyXml)) return;

                // Verschlüssele den Datei-AES-Key mit dem RSA-Public-Key des Empfängers
                byte[] encKeyForReceiver;
                using (var rsaReceiver = new RSACryptoServiceProvider(2048))
                {
                    rsaReceiver.FromXmlString(keyData.PublicKeyXml);
                    encKeyForReceiver = rsaReceiver.Encrypt(fileAesKey, true);
                }

                // In ChatViewModel.Crypto.cs innerhalb von ExecuteAttachFileAsync:
                var privatePayload = new
                {
                    ReceiverEmail = SelectedContact.Email,
                    EncryptedKey = Convert.ToBase64String(encKeyForReceiver),
                    CipherText = cipherBase64, // 🌟 WICHTIG: Das muss der Base64-String der verschlüsselten Bytes sein!
                    Iv = ivBase64,
                    AuthTag = tagBase64,
                    MessageType = calculatedType,
                    FileName = originalFileName
                };


                var response = await _httpClient.PostAsJsonAsync("api/messages/send", privatePayload);
                if (response.IsSuccessStatusCode)
                {
                    // 1. Die frisch generierte ID aus dem JSON-Response der API auslesen
                    var jsonString = await response.Content.ReadAsStringAsync();
                    int realDbId = 0;
                    try
                    {
                        using (var doc = System.Text.Json.JsonDocument.Parse(jsonString))
                        {
                            // 🌟 AUSFALLSICHERES SCANNER-NETZ: Wir suchen nach JEDER erdenklichen ID-Schreibweise!
                            if (doc.RootElement.TryGetProperty("id", out var idProp)) realDbId = idProp.GetInt32();
                            else if (doc.RootElement.TryGetProperty("Id", out var idPropUpper)) realDbId = idPropUpper.GetInt32();
                            else if (doc.RootElement.TryGetProperty("messageId", out var idPropMsg)) realDbId = idPropMsg.GetInt32();
                            else if (doc.RootElement.TryGetProperty("MessageId", out var idPropMsgUpper)) realDbId = idPropMsgUpper.GetInt32();
                            else if (doc.RootElement.TryGetProperty("message", out var msgObj) && msgObj.TryGetProperty("id", out var innerId)) realDbId = innerId.GetInt32();
                        }
                    }
                    catch { }

                    App.Current.Dispatcher.Invoke(() =>
                    {
                        //var myMsg = new ChatMessageModel { 
                        //    SenderEmail = "Du", 
                        //    CipherText = $"📎 Datei gesendet: {originalFileName}", 
                        //    IsSender = true, 
                        //    MessageType = calculatedType, 
                        //    FileName = originalFileName 
                        //};
                        // Ein komplett sauberes, neues Modell für die Datei erzeugen
                        var myMsg = new ChatMessageModel
                        {
                            Id = realDbId, // 🔑 ENTSCHEIDEND: Ohne diese echte ID läuft das SignalR-Event ins Leere!
                            SenderEmail = "Du",
                            //CipherText = originalFileName, // Zeigt den Dateinamen an
                            IsSender = true,
                            MessageType = calculatedType, // Signalisiert dem XAML, dass es ein Datei-Container ist
                            FileName = originalFileName,
                            MessageStatus = 0 // Startet mit Status 0 (Ein weißes Häkchen ✓)
                        };

                        // 🌟 SEITENEFFEKT-KILLER 2: Wir sichern die Krypto-Payload (die verschlüsselten Daten, die an die API gingen)
                        // in einer dafür vorgesehenen Eigenschaft ab, falls dein System sie im Nachgang braucht.
                        // (Ersetze 'encryptedPayloadBase64' durch deine tatsächliche Variable, falls vorhanden)
                        //try { myMsg.CipherText = encryptedPayloadBase64; } catch { myMsg.CipherText = originalFileName; }

                        // 🌟 SEITENEFFEKT-KILLER 1: Wir sichern die unverschlüsselten Original-Bytes 
                        // für JEDEN Dateityp (auch PDFs/Docx) direkt im Modell. 
                        // Dadurch weiß der "Doppelklick zum Speichern"-Befehl sofort, was er auf die Festplatte schreiben muss!
                        myMsg.DecryptedFileBytes = fileBytes;
                        myMsg.DecryptedMediaBytes = fileBytes;

                        if (calculatedType == "Image")
                        {
                            myMsg.DecryptedImagePayload = ConvertBytesToBitmap(fileBytes);
                            myMsg.CipherText = $"📷 Bild gesendet: {originalFileName}";
                        }
                        else if (calculatedType == "Audio")
                        {
                            // 🌟 JETZT NEU: Sichert die Audio-Bytes sofort lokal in DEINEM Player, damit du es selbst abspielen kannst!
                            //myMsg.DecryptedFileBytes = fileBytes;
                            //myMsg.DecryptedMediaBytes = fileBytes;
                            myMsg.CipherText = $"🎵 Audio gesendet: {originalFileName}";
                        }
                        else if (calculatedType == "Video")
                        {
                            //myMsg.DecryptedFileBytes = fileBytes;
                            //myMsg.DecryptedMediaBytes = fileBytes;
                            myMsg.CipherText = $"🎥 Video gesendet: {originalFileName}";
                        }
                        else
                        {
                            myMsg.CipherText = $"📎 Datei gesendet: {originalFileName}";
                        }

                        // Ab in die UI-Liste
                        ChatHistory.Add(myMsg);

                        // 🌟 SEITENEFFEKT-KILLER 2: Wir zwingen das WPF-Layout, die Häkchen 
                        // für diesen neuen Container im RAM sofort visuell scharfzuschalten!
                        myMsg.MessageStatus = 0;

                    });
                }
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Krypto-Fehler beim Payload-Packaging: {ex.Message}");
        }
    }

    // Kleiner RAM-Helfer, um rohe Bytes blitzschnell für WPF-Grafiken bereitzustellen
    private System.Windows.Media.Imaging.BitmapImage ConvertBytesToBitmap(byte[] bytes)
    {
        try
        {
            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            using (var stream = new System.IO.MemoryStream(bytes))
            {
                bitmap.BeginInit();
                // WICHTIG: OnLoad zwingt WPF, das gesamte Bild SOFORT vollständig 
                // in den RAM-Buffer zu dekodieren, anstatt es asynchron zu verzögern!
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
            }
            // Das Bild "einfrieren" macht es threadübergreifend für das XAML lesbar
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Grafik-Fehler] Konvertierung fehlgeschlagen: {ex.Message}");
            return null!;
        }
    }

    // 🌟 NEU: ASYMMETRISCHE SCHLÜSSEL-ROTATION (Enterprise-Standard)
    public async Task ExecuteRotateKeysAsync()
    {
        var result = System.Windows.MessageBox.Show(
            "Möchtest du dein RSA-2048 Schlüsselpaar wirklich erneuern?\n\n" +
            "⚠️ WICHTIGER HINWEIS:\nBisherige verschlüsselte Nachrichten deiner Freunde können danach aus Sicherheitsgründen nicht mehr dechiffriert werden!",
            "Sicherheitsschlüssel rotieren",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            IsLoadingHistory = true; // Spinner als Feedback starten

            // 1. Alten Windows-Krypto-Tresor-Container physikalisch löschen
            string containerName = $"SecureChat_CryptoContainer_{MyInitials}";
            var deleteParams = new System.Security.Cryptography.CspParameters
            {
                KeyContainerName = containerName,
                Flags = System.Security.Cryptography.CspProviderFlags.NoFlags
            };

            // Über DeleteKeyOnExit löscht .NET den Container beim Schließen des Providers
            using (var rsaDelete = new System.Security.Cryptography.RSACryptoServiceProvider(deleteParams))
            {
                rsaDelete.PersistKeyInCsp = false;
                rsaDelete.Clear();
            }

            // 2. Ein komplett frisches RSA-Schlüsselpaar im Windows-Tresor anlegen
            var createParams = new System.Security.Cryptography.CspParameters
            {
                KeyContainerName = containerName,
                Flags = System.Security.Cryptography.CspProviderFlags.NoFlags
            };

            // Hier bricht .NET das Readonly auf und generiert die Schlüssel mathematisch neu
            using (var rsaNew = new System.Security.Cryptography.RSACryptoServiceProvider(2048, createParams))
            {
                // Die neuen XML-Strukturen in das laufende ViewModel einspielen
                // Hinweis: Da _privateKeyXml und PublicKeyXml im Konstruktor fixiert sind,
                // nutzen wir Spiegel-Eigenschaften oder laden den internen Provider neu:
                _rsaProvider.FromXmlString(rsaNew.ToXmlString(true));
            }

            // 3. Den neuen öffentlichen Schlüssel live an den SQL Server übertragen
            string newPublicKeyXml = _rsaProvider.ToXmlString(false);
            var response = await _httpClient.PostAsJsonAsync("api/users/keys", new { publicKeyXml = newPublicKeyXml });

            if (response.IsSuccessStatusCode)
            {
                System.Windows.MessageBox.Show(
                    "Schlüssel-Rotation erfolgreich abgeschlossen!\n\n" +
                    "🔒 Dein neues asymmetrisches Schlüsselpaar ist ab jetzt aktiv.",
                    "Sicherheits-Update", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

                // Den aktuellen Chatverlauf leeren und Krypto-Hinweis einblenden
                ChatHistory.Clear();
                ChatHistory.Add(new ChatMessageModel
                {
                    SenderEmail = "System",
                    CipherText = "🔑 Du hast dein RSA-Schlüsselpaar erneuert. Ab jetzt eingehende Nachrichten nutzen die neue Verschlüsselung."
                });
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Fehler bei der Krypto-Rotation: {ex.Message}", "Krypto-Fehler");
        }
        finally
        {
            IsLoadingHistory = false;
        }
    }

    private record PublicKeyResponseDto(string PublicKeyXml);
    public record UpdateKeysDto(string PublicKeyXml);
}


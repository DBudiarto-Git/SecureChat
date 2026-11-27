using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace SecureChat.Desktop;

public class BytesToUriConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is byte[] bytes && bytes.Length > 0)
        {
            try
            {
                // Standard-Fallback-Endung
                string extension = ".wav";

                // MAGIC BYTES LIVE-ANALYSE IM RAM:
                // MP3-Dateien beginnen fast immer mit "ID3" (0x49 0x44 0x33) oder Frame-Sync (0xFF 0xFB / 0xF3)
                if (bytes.Length > 4)
                {
                    // 1. MP3 Erkennung (bestehend)
                    if ((bytes[0] == 0x49 && bytes[1] == 0x44 && bytes[2] == 0x33) ||
                        (bytes[0] == 0xFF && (bytes[1] == 0xFB || bytes[1] == 0xF3 || bytes[1] == 0xF2)))
                    {
                        extension = ".mp3";
                    }
                    // 2. 🎥 NEU: MP4 / MOV Erkennung (Magic Bytes für ISO-Medien: "ftyp")
                    // MP4-Dateien haben meist ab Byte 4 die Zeichen 'f','t','y','p'
                    else if (bytes[4] == 0x66 && bytes[5] == 0x74 && bytes[6] == 0x79 && bytes[7] == 0x70)
                    {
                        extension = ".mp4"; // Deckt MP4 und die meisten MOVs ab
                    }
                }

                // 🌟 DIE RETTUNG: Wir verpassen der temporären RAM-Datei die ECHTE Endung!
                string tempFile = Path.Combine(Path.GetTempPath(), $"SecureChat_Media_{Guid.NewGuid():N}{extension}");

                // Datei unbeschädigt schreiben
                File.WriteAllBytes(tempFile, bytes);

                // Gibt die URI mit korrekter Endung an das MediaElement zurück
                return new Uri(tempFile);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Media-Converter-Fehler] {ex.Message}");
            }
        }
        return null!;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

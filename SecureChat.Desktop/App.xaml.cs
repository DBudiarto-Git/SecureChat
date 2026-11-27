using SecureChat.Desktop.ViewModels;
using SecureChat.Desktop.Views;
using System;
using System.Windows;

namespace SecureChat.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // 🌟 DER INSTANZ-BEWEIS: Jede App-Instanz generiert im RAM einen weltweit einzigartigen Code!
        string uniqueInstanceId = Guid.NewGuid().ToString().Substring(0, 8).ToUpper();

        // Wir schreiben den Beweis sofort live in das Visual Studio Ausgabefenster
        System.Diagnostics.Debug.WriteLine($"=======================================================");
        System.Diagnostics.Debug.WriteLine($"[KRYPTO-START] Neue App-Instanz initialisiert! ID: #{uniqueInstanceId}");
        System.Diagnostics.Debug.WriteLine($"=======================================================");

        // 1. Zuerst die normale WPF-Basis laden
        base.OnStartup(e);

        // 🌟 DIE RETTUNG: Intelligentes Einfrieren OHNE nerviges Debugger.cs-Popup!
        // Wir prüfen, ob der Debugger bereits läuft (das ist bei User A der Fall).
        // Wenn er NICHT läuft (das ist bei der .exe von User B der Fall), frieren wir den Prozess ein!
        if (!System.Diagnostics.Debugger.IsAttached)
        {
            //System.Windows.MessageBox.Show(
            //    $"Krypto-Instanz #{uniqueInstanceId} im RAM eingefroren.\n\n" +
            //    "Verfahren wie gewohnt:\n" +
            //    "1. Gehe in dein offenes Visual Studio.\n" +
            //    "2. Klicke auf 'Debuggen' -> 'An Prozess anhängen...'.\n" +
            //    "3. Wähle die neue 'SecureChat.Desktop.exe' aus.\n" +
            //    "4. Klicke danach hier auf OK!",
            //    "🔒 Symmetrie-Scope-Bremse",
            //    MessageBoxButton.OK,
            //    MessageBoxImage.Information);
        }

        // Wir instanziieren und öffnen das Fenster direkt im Arbeitsspeicher
        //var authWindow = new AuthWindow();
        //authWindow.Show();
    }

    // Globaler Register-Anker für dein aktives ViewModel (von gestern)
    public static ChatViewModel? MainViewModel { get; set; }

    //// 🌟 DIE RETTUNG: Diese Signatur passt mathematisch zu 100% zum WPF-StartupEventHandler!
    //public App()
    //{
    //    // 1. 🌟 DIE DEBUG-BREMSE: Friert die zweite .exe beim Doppelklick sofort ein,
    //    // damit du ab Sekunde Null live mit Breakpoints debuggen kannst!
    //    System.Diagnostics.Debugger.Launch();

    //    // 2. Das Anmeldefenster frisch instanziieren und anzeigen
    //    var authWindow = new AuthWindow();
    //    authWindow.Show();
    //}

}



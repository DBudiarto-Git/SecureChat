using SecureChat.Desktop.Models;
using SecureChat.Desktop.ViewModels;
using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace SecureChat.Desktop.Views;

public partial class ChatWindow : Window
{
    public ChatWindow(ChatViewModel viewModel)
    {
        InitializeComponent();

        // 🌟 DIE ULTIMATIVE RETTUNG FÜR SITUATION 1:
        // Jedes Mal, wenn dieses Fenster geladen und auf dem Bildschirm angezeigt wird,
        // zwingen wir das ViewModel, die ungelesenen Nachrichten live als gelesen zu markieren!
        //this.Loaded += ChatWindow_Loaded;

        // Wir binden das übergebene ViewModel fest an die Oberfläche
        this.DataContext = viewModel;

        // ===================================================================
        // 🔍 REVOLUTIONÄRER HIT-TEST-DIAGNOSE-CODE FÜR USER B (HIER EINGEBAUT)
        // ===================================================================
        this.PreviewMouseDown += (sender, e) =>
        {
            var hitElement = e.OriginalSource;
            if (hitElement != null)
            {
                System.Diagnostics.Debug.WriteLine($"[Hit-Test] User B klickt auf Element-Typ: {hitElement.GetType().Name}");
            }
        };

        // ===================================================================

        //  AUTOMATISCHER SCROLL-TRICK:
        // Wir lauschen auf jede Änderung in der ChatHistory-Liste
        if (viewModel.ChatHistory is INotifyCollectionChanged collection)
        {
            collection.CollectionChanged += (s, e) =>
            {
                // Sobald ein neues Element hinzugefügt wird, springen wir ans Ende
                if (e.Action == NotifyCollectionChangedAction.Add && viewModel.ChatHistory.Count > 0)
                {
                    // Wir wechseln kurz auf den UI-Thread, um dem Grafik-System Zeit zum Zeichnen zu geben
                    this.Dispatcher.InvokeAsync(() =>
                    {
                        var lastItem = viewModel.ChatHistory[viewModel.ChatHistory.Count - 1];
                        ChatList.ScrollIntoView(lastItem);
                    });
                }
            };
        }
    }

    private void GroupListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Falls das Event durch das Leeren/Nullen gefeuert wird, sicher abbrechen
        if (e.AddedItems == null || e.AddedItems.Count == 0) return;

        // Das geklickte Gruppen-Objekt sicher als ChatGroupModel auflösen
        if (e.AddedItems[0] is ChatGroupModel clickedGroup)
        {
            System.Diagnostics.Debug.WriteLine($"[Krypto-WPF-Sicherheit] Gruppe geklickt: {clickedGroup.GroupName}");

            // 🌟 FIX: Auf den echten ViewModel-Typen (ChatViewModel) casten!
            if (this.DataContext is ChatViewModel vm)
            {
                // Wir zwingen den Setter direkt im Main-UI-Thread
                vm.SelectedGroup = clickedGroup;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[WPF-Klick-Fehler] DataContext ist kein ChatViewModel!");
            }
        }
    }

    // In ChatWindow.xaml.cs innerhalb der Klasse ChatWindow:
    private void VideoCorePlayer_MediaEnded(object sender, System.Windows.RoutedEventArgs e)
    {
        // 🌟 THREAD-GARANTIE: Führt den Reset direkt im Kontext des UI-Threads aus!
        App.Current.Dispatcher.Invoke(() =>
        {
            if (DataContext is SecureChat.Desktop.ViewModels.ChatViewModel)
            {
                // Ruft unsere neue, harte Reset-Logik in C# auf
                SecureChat.Desktop.ViewModels.ChatViewModel.ResetAndStopEntirePlayback();
            }
        });
    }

    // Globales Flag, damit der Timer weiß, dass wir gerade spulen
    public static bool IsUserDraggingSlider = false;

    private void Slider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        // Schaltet das Flag direkt in deinem ChatViewModel (oder der partialen Gruppe) um
        SecureChat.Desktop.ViewModels.ChatViewModel.IsUserDraggingSlider = true;
    }

    private void Slider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (sender is System.Windows.Controls.Slider slider && slider.DataContext is SecureChat.Desktop.Models.ChatMessageModel msg)
        {
            if (DataContext is SecureChat.Desktop.ViewModels.ChatViewModel vm)
            {
                vm.SpoolMediaToPosition(msg, slider.Value);
            }
        }
        // Ziehvorgang beendet
        SecureChat.Desktop.ViewModels.ChatViewModel.IsUserDraggingSlider = false;
    }

    // In ChatWindow.xaml.cs innerhalb der Klasse ChatWindow:
    private async void YouTubeWebView_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is Microsoft.Web.WebView2.Wpf.WebView2 webView)
        {
            try
            {
                // 1. Erst initialisieren, falls das noch nicht geschehen ist
                if (webView.CoreWebView2 == null)
                {
                    await webView.EnsureCoreWebView2Async(null);
                }

                // 2. Jetzt greifen wir uns den Datenkontext (die Nachricht) ab
                if (webView.DataContext is SecureChat.Desktop.Models.ChatMessageModel msg && !string.IsNullOrEmpty(msg.YouTubeVideoId))
                {
                    string embedUrl = "https://youtube.com/embed/" + msg.YouTubeVideoId;

                    // 3. Nur navigieren, wenn wir nicht schon auf dieser Seite sind
                    if (webView.CoreWebView2.Source != embedUrl)
                    {
                        webView.CoreWebView2.Navigate(embedUrl);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebView2-Sicherheits-Fehler] {ex.Message}");
            }
        }
    }
}


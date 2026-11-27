using System.Windows;
using SecureChat.Desktop.ViewModels; // Stelle sicher, dass der Pfad zu deinem AuthViewModel passt

namespace SecureChat.Desktop.Views
{
    /// <summary>
    /// Interaktionslogik für AuthWindow.xaml
    /// </summary>
    public partial class AuthWindow : Window
    {
        public AuthWindow()
        {
            InitializeComponent();

            // 🌟 DIE RETTUNG TEIL 2: Harte, garantierte Neu-Instanziierung bei JEDEM Fensterstart!
            // Jetzt wird der Konstruktor von AuthViewModel bei jedem User unerbittlich neu aufgerufen!
            this.DataContext = new AuthViewModel();
        }
    }
}

using QuickMigrate.ViewModels;
using System.Windows;

namespace QuickMigrate
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}
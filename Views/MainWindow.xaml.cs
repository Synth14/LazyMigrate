using LazyMigrate.ViewModels;
using System.Windows;

namespace LazyMigrate
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QuickMigrate.Models
{
    public class SoftwareInfo : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _includeSettings;
        private string _status = "Détecté";

        public string Name { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string InstallPath { get; set; } = string.Empty;
        public string UninstallString { get; set; } = string.Empty;
        public DateTime InstallDate { get; set; }
        public long EstimatedSize { get; set; }
        public string Category { get; set; } = "Inconnu";
        public string IconPath { get; set; } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public bool IncludeSettings
        {
            get => _includeSettings;
            set
            {
                _includeSettings = value;
                OnPropertyChanged();
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
            }
        }

        public List<string> ExecutablePaths { get; set; } = new();
        public List<SettingsPath> SettingsPaths { get; set; } = new();
        public List<DownloadSource> DownloadSources { get; set; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }



}
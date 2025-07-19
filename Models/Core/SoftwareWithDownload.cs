namespace LazyMigrate.Models.Download
{
    /// <summary>
    /// Modèle principal pour un logiciel avec gestion des téléchargements et settings
    /// </summary>
    public class SoftwareWithDownload : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _includeSettings;
        private string _status = "Détecté";

        #region Propriétés de base du logiciel
        public string Name { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string InstallPath { get; set; } = string.Empty;
        public string UninstallString { get; set; } = string.Empty;
        public DateTime InstallDate { get; set; }
        public long EstimatedSize { get; set; }
        public string Category { get; set; } = "Inconnu";
        public string IconPath { get; set; } = string.Empty;

        public List<string> ExecutablePaths { get; set; } = new();
        public List<DownloadSourceInfo> DownloadSources { get; set; } = new();
        #endregion

        #region Propriétés Settings
        public List<SettingsPath> SettingsPaths { get; set; } = new();

        public string SettingsStatus
        {
            get
            {
                if (SettingsPaths?.Any() == true)
                {
                    return $"✅ {SettingsPaths.Count}";
                }
                return "❌ Aucun";
            }
        }
        #endregion

        #region Propriétés UI et Export
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
        #endregion

        #region Propriétés Download
        public DownloadStatus DownloadStatus { get; set; } = DownloadStatus.NotSearched;
        public DownloadResult? DownloadResult { get; set; }

        public string DownloadStatusText => DownloadStatus switch
        {
            DownloadStatus.NotSearched => "⚪ Non cherché",
            DownloadStatus.Searching => "🔍 Recherche...",
            DownloadStatus.Found => $"✅ {DownloadResult?.TotalLinksFound} lien(s)",
            DownloadStatus.NotFound => "❌ Aucun lien",
            DownloadStatus.Error => "⚠️ Erreur",
            _ => "❓ Inconnu"
        };

        public string BestDownloadUrl
        {
            get
            {
                var bestLink = DownloadResult?.BestDownloadLink;
                if (bestLink?.IsValid == true && !string.IsNullOrEmpty(bestLink.DownloadUrl))
                {
                    return bestLink.DownloadUrl;
                }
                return "";
            }
        }

        public string BestDownloadDisplayText
        {
            get
            {
                var bestLink = DownloadResult?.BestDownloadLink;
                if (bestLink?.IsValid == true && !string.IsNullOrEmpty(bestLink.DownloadUrl))
                {
                    // Afficher le nom du site ou un texte court
                    var uri = new Uri(bestLink.DownloadUrl);
                    var domain = uri.Host.Replace("www.", "");
                    return $"📥 {domain}";
                }
                return "Aucun lien";
            }
        }
        #endregion

        #region Propriétés calculées
        public string EstimatedSizeFormatted
        {
            get
            {
                if (EstimatedSize <= 0) return "Inconnue";

                string[] sizes = { "B", "KB", "MB", "GB", "TB" };
                double len = EstimatedSize;
                int order = 0;

                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len /= 1024;
                }

                return $"{len:0.##} {sizes[order]}";
            }
        }
        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
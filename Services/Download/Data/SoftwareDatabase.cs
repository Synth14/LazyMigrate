namespace LazyMigrate.Services.Download
{
    /// <summary>
    /// Base de données de logiciels en mémoire avec recherche optimisée
    /// </summary>
    public class SoftwareDatabase
    {
        private readonly HashSet<SoftwareEntry> _entries;
        private readonly Dictionary<string, HashSet<SoftwareEntry>> _nameIndex;
        private readonly Dictionary<string, HashSet<SoftwareEntry>> _publisherIndex;
        private readonly Dictionary<string, HashSet<SoftwareEntry>> _keywordIndex;

        public SoftwareDatabase()
        {
            _entries = new HashSet<SoftwareEntry>();
            _nameIndex = new Dictionary<string, HashSet<SoftwareEntry>>(StringComparer.OrdinalIgnoreCase);
            _publisherIndex = new Dictionary<string, HashSet<SoftwareEntry>>(StringComparer.OrdinalIgnoreCase);
            _keywordIndex = new Dictionary<string, HashSet<SoftwareEntry>>(StringComparer.OrdinalIgnoreCase);

            InitializeDatabase();
        }

        /// <summary>
        /// Recherche rapide avec scoring automatique
        /// </summary>
        public List<SoftwareEntry> FindMatches(string softwareName, string publisher = "")
        {
            var candidates = new Dictionary<SoftwareEntry, int>();
            var nameLower = softwareName.ToLowerInvariant();
            var publisherLower = publisher?.ToLowerInvariant() ?? "";

            // 1. Recherche exacte par nom (score +100)
            if (_nameIndex.TryGetValue(nameLower, out var exactMatches))
            {
                foreach (var entry in exactMatches)
                    candidates[entry] = candidates.GetValueOrDefault(entry, 0) + 100;
            }

            // 2. Recherche par publisher (score +50)
            if (!string.IsNullOrEmpty(publisherLower) && _publisherIndex.TryGetValue(publisherLower, out var publisherMatches))
            {
                foreach (var entry in publisherMatches)
                    candidates[entry] = candidates.GetValueOrDefault(entry, 0) + 50;
            }

            // 3. Recherche par mots-clés (score +20 par mot)
            var words = ExtractWords(nameLower);
            foreach (var word in words)
            {
                if (_keywordIndex.TryGetValue(word, out var keywordMatches))
                {
                    foreach (var entry in keywordMatches)
                        candidates[entry] = candidates.GetValueOrDefault(entry, 0) + 20;
                }
            }

            // 4. Recherche fuzzy dans les noms (score +10)
            foreach (var entry in _entries)
            {
                if (!candidates.ContainsKey(entry))
                {
                    if (entry.AlternativeNames.Any(alt => alt.Contains(nameLower) || nameLower.Contains(alt)))
                    {
                        candidates[entry] = 10;
                    }
                }
            }

            // Retourner les résultats triés par score
            return candidates.OrderByDescending(kvp => kvp.Value)
                           .Take(5)
                           .Select(kvp => kvp.Key)
                           .ToList();
        }

        private void InitializeDatabase()
        {
            var entries = new[]
            {
                // Navigateurs
                new SoftwareEntry
                {
                    Name = "Google Chrome",
                    Publisher = "Google",
                    OfficialSite = "https://www.google.com/chrome/",
                    DownloadUrl = "https://www.google.com/chrome/browser/desktop/index.html",
                    AlternativeNames = new[] { "chrome", "google chrome" },
                    Keywords = new[] { "browser", "navigateur", "google" },
                    FileInfo = new DownloadFileInfo { FileName = "ChromeSetup.exe", FileType = "exe" },
                    Confidence = 0.95
                },

                new SoftwareEntry
                {
                    Name = "Mozilla Firefox",
                    Publisher = "Mozilla",
                    OfficialSite = "https://www.mozilla.org/firefox/",
                    DownloadUrl = "https://www.mozilla.org/firefox/download/thanks/",
                    AlternativeNames = new[] { "firefox", "mozilla firefox" },
                    Keywords = new[] { "browser", "navigateur", "mozilla" },
                    FileInfo = new DownloadFileInfo { FileName = "Firefox Setup.exe", FileType = "exe" },
                    Confidence = 0.95
                },

                new SoftwareEntry
                {
                    Name = "Microsoft Edge",
                    Publisher = "Microsoft",
                    OfficialSite = "https://www.microsoft.com/edge",
                    DownloadUrl = "https://www.microsoft.com/edge/download",
                    AlternativeNames = new[] { "edge", "microsoft edge" },
                    Keywords = new[] { "browser", "navigateur", "microsoft" },
                    FileInfo = new DownloadFileInfo { FileName = "MicrosoftEdgeSetup.exe", FileType = "exe" },
                    Confidence = 0.95
                },

                // Développement
                new SoftwareEntry
                {
                    Name = "Visual Studio Code",
                    Publisher = "Microsoft",
                    OfficialSite = "https://code.visualstudio.com/",
                    DownloadUrl = "https://code.visualstudio.com/download",
                    AlternativeNames = new[] { "vscode", "vs code", "visual studio code", "code" },
                    Keywords = new[] { "editor", "ide", "development", "microsoft", "code" },
                    FileInfo = new DownloadFileInfo { FileName = "VSCodeSetup.exe", FileType = "exe" },
                    Confidence = 0.95
                },

                new SoftwareEntry
                {
                    Name = "Git",
                    Publisher = "Git SCM",
                    OfficialSite = "https://git-scm.com/",
                    DownloadUrl = "https://git-scm.com/download/win",
                    AlternativeNames = new[] { "git", "git scm" },
                    Keywords = new[] { "version control", "scm", "development" },
                    FileInfo = new DownloadFileInfo { FileName = "Git-Setup.exe", FileType = "exe" },
                    Confidence = 0.95
                },

                new SoftwareEntry
                {
                    Name = "Notepad++",
                    Publisher = "Notepad++ Team",
                    OfficialSite = "https://notepad-plus-plus.org/",
                    DownloadUrl = "https://notepad-plus-plus.org/downloads/",
                    AlternativeNames = new[] { "notepad++", "notepad plus plus", "npp" },
                    Keywords = new[] { "editor", "text", "development" },
                    FileInfo = new DownloadFileInfo { FileName = "npp.Installer.exe", FileType = "exe" },
                    Confidence = 0.95
                },

                // Utilitaires
                new SoftwareEntry
                {
                    Name = "7-Zip",
                    Publisher = "Igor Pavlov",
                    OfficialSite = "https://www.7-zip.org/",
                    DownloadUrl = "https://www.7-zip.org/download.html",
                    AlternativeNames = new[] { "7zip", "7-zip", "sevenzip" },
                    Keywords = new[] { "archive", "compression", "zip" },
                    FileInfo = new DownloadFileInfo { FileName = "7z-x64.exe", FileType = "exe" },
                    Confidence = 0.95
                },

                new SoftwareEntry
                {
                    Name = "WinRAR",
                    Publisher = "RARLAB",
                    OfficialSite = "https://www.win-rar.com/",
                    DownloadUrl = "https://www.win-rar.com/download.html",
                    AlternativeNames = new[] { "winrar", "rar" },
                    Keywords = new[] { "archive", "compression", "rar" },
                    FileInfo = new DownloadFileInfo { FileName = "winrar-x64.exe", FileType = "exe" },
                    Confidence = 0.95
                },

                new SoftwareEntry
                {
                    Name = "VLC Media Player",
                    Publisher = "VideoLAN",
                    OfficialSite = "https://www.videolan.org/vlc/",
                    DownloadUrl = "https://www.videolan.org/vlc/download-windows.html",
                    AlternativeNames = new[] { "vlc", "vlc media player", "videolan" },
                    Keywords = new[] { "media", "player", "video", "audio" },
                    FileInfo = new DownloadFileInfo { FileName = "vlc-win64.exe", FileType = "exe" },
                    Confidence = 0.95
                },

                // Communication
                new SoftwareEntry
                {
                    Name = "Discord",
                    Publisher = "Discord Inc.",
                    OfficialSite = "https://discord.com/",
                    DownloadUrl = "https://discord.com/download",
                    AlternativeNames = new[] { "discord" },
                    Keywords = new[] { "chat", "voice", "communication", "gaming" },
                    FileInfo = new DownloadFileInfo { FileName = "DiscordSetup.exe", FileType = "exe" },
                    Confidence = 0.95
                },

                new SoftwareEntry
                {
                    Name = "Skype",
                    Publisher = "Microsoft",
                    OfficialSite = "https://www.skype.com/",
                    DownloadUrl = "https://www.skype.com/get-skype/",
                    AlternativeNames = new[] { "skype" },
                    Keywords = new[] { "video call", "communication", "microsoft" },
                    FileInfo = new DownloadFileInfo { FileName = "SkypeSetup.exe", FileType = "exe" },
                    Confidence = 0.95
                },

                // Gaming
                new SoftwareEntry
                {
                    Name = "Steam",
                    Publisher = "Valve Corporation",
                    OfficialSite = "https://store.steampowered.com/",
                    DownloadUrl = "https://store.steampowered.com/about/",
                    AlternativeNames = new[] { "steam" },
                    Keywords = new[] { "gaming", "games", "valve", "launcher" },
                    FileInfo = new DownloadFileInfo { FileName = "SteamSetup.exe", FileType = "exe" },
                    Confidence = 0.95
                },

                // Adobe et créatifs
                new SoftwareEntry
                {
                    Name = "Adobe Creative Cloud",
                    Publisher = "Adobe",
                    OfficialSite = "https://www.adobe.com/",
                    DownloadUrl = "https://creativecloud.adobe.com/apps/download/creative-cloud",
                    AlternativeNames = new[] { "adobe", "creative cloud", "adobe cc" },
                    Keywords = new[] { "creative", "design", "photo", "video" },
                    FileInfo = new DownloadFileInfo { FileName = "Creative_Cloud_Set-Up.exe", FileType = "exe" },
                    Confidence = 0.80
                },

                // Microsoft Office
                new SoftwareEntry
                {
                    Name = "Microsoft Office",
                    Publisher = "Microsoft",
                    OfficialSite = "https://www.microsoft.com/microsoft-365",
                    DownloadUrl = "https://www.microsoft.com/microsoft-365/buy/microsoft-365",
                    AlternativeNames = new[] { "office", "microsoft office", "office 365", "microsoft 365" },
                    Keywords = new[] { "office", "word", "excel", "powerpoint", "microsoft" },
                    FileInfo = new DownloadFileInfo { FileName = "OfficeSetup.exe", FileType = "exe" },
                    Confidence = 0.70
                },

                // Plus de développement
                new SoftwareEntry
                {
                    Name = "Visual Studio",
                    Publisher = "Microsoft",
                    OfficialSite = "https://visualstudio.microsoft.com/",
                    DownloadUrl = "https://visualstudio.microsoft.com/downloads/",
                    AlternativeNames = new[] { "visual studio", "vs", "visual studio community" },
                    Keywords = new[] { "ide", "development", "microsoft", "coding" },
                    FileInfo = new DownloadFileInfo { FileName = "vs_community.exe", FileType = "exe" },
                    Confidence = 0.90
                },

                new SoftwareEntry
                {
                    Name = "GitHub Desktop",
                    Publisher = "GitHub",
                    OfficialSite = "https://desktop.github.com/",
                    DownloadUrl = "https://central.github.com/deployments/desktop/desktop/latest/win32",
                    AlternativeNames = new[] { "github desktop", "github" },
                    Keywords = new[] { "git", "github", "version control", "development" },
                    FileInfo = new DownloadFileInfo { FileName = "GitHubDesktopSetup.exe", FileType = "exe" },
                    Confidence = 0.90
                },

                // Plus d'utilitaires
                new SoftwareEntry
                {
                    Name = "CCleaner",
                    Publisher = "Piriform",
                    OfficialSite = "https://www.ccleaner.com/",
                    DownloadUrl = "https://www.ccleaner.com/ccleaner/download",
                    AlternativeNames = new[] { "ccleaner" },
                    Keywords = new[] { "cleaner", "optimization", "registry" },
                    FileInfo = new DownloadFileInfo { FileName = "ccsetup.exe", FileType = "exe" },
                    Confidence = 0.85
                },

                new SoftwareEntry
                {
                    Name = "FileZilla",
                    Publisher = "FileZilla Project",
                    OfficialSite = "https://filezilla-project.org/",
                    DownloadUrl = "https://filezilla-project.org/download.php?type=client",
                    AlternativeNames = new[] { "filezilla", "ftp" },
                    Keywords = new[] { "ftp", "file transfer", "client" },
                    FileInfo = new DownloadFileInfo { FileName = "FileZilla_setup.exe", FileType = "exe" },
                    Confidence = 0.90
                },

                // Plus de communication
                new SoftwareEntry
                {
                    Name = "Zoom",
                    Publisher = "Zoom",
                    OfficialSite = "https://zoom.us/",
                    DownloadUrl = "https://zoom.us/download",
                    AlternativeNames = new[] { "zoom" },
                    Keywords = new[] { "video call", "meeting", "conference" },
                    FileInfo = new DownloadFileInfo { FileName = "ZoomInstaller.exe", FileType = "exe" },
                    Confidence = 0.95
                },

                new SoftwareEntry
                {
                    Name = "Microsoft Teams",
                    Publisher = "Microsoft",
                    OfficialSite = "https://www.microsoft.com/teams/",
                    DownloadUrl = "https://www.microsoft.com/teams/download-app",
                    AlternativeNames = new[] { "teams", "microsoft teams" },
                    Keywords = new[] { "teams", "meeting", "collaboration", "microsoft" },
                    FileInfo = new DownloadFileInfo { FileName = "Teams_windows_x64.exe", FileType = "exe" },
                    Confidence = 0.90
                },

                // Plus de gaming
                new SoftwareEntry
                {
                    Name = "Battle.net",
                    Publisher = "Blizzard Entertainment",
                    OfficialSite = "https://www.blizzard.com/",
                    DownloadUrl = "https://www.battle.net/download/getInstallerForGame?os=win&gameProgram=BATTLENET_APP",
                    AlternativeNames = new[] { "battle.net", "battlenet", "blizzard" },
                    Keywords = new[] { "gaming", "blizzard", "launcher", "wow" },
                    FileInfo = new DownloadFileInfo { FileName = "Battle.net-Setup.exe", FileType = "exe" },
                    Confidence = 0.90
                },

                new SoftwareEntry
                {
                    Name = "Origin",
                    Publisher = "Electronic Arts",
                    OfficialSite = "https://www.origin.com/",
                    DownloadUrl = "https://www.origin.com/download",
                    AlternativeNames = new[] { "origin", "ea origin" },
                    Keywords = new[] { "gaming", "ea", "electronic arts", "launcher" },
                    FileInfo = new DownloadFileInfo { FileName = "OriginSetup.exe", FileType = "exe" },
                    Confidence = 0.90
                },

                // Sécurité
                new SoftwareEntry
                {
                    Name = "Malwarebytes",
                    Publisher = "Malwarebytes",
                    OfficialSite = "https://www.malwarebytes.com/",
                    DownloadUrl = "https://www.malwarebytes.com/mwb-download",
                    AlternativeNames = new[] { "malwarebytes", "mbam" },
                    Keywords = new[] { "antivirus", "security", "malware" },
                    FileInfo = new DownloadFileInfo { FileName = "MBSetup.exe", FileType = "exe" },
                    Confidence = 0.85
                }
            };

            foreach (var entry in entries)
            {
                AddEntry(entry);
            }
        }

        private void AddEntry(SoftwareEntry entry)
        {
            _entries.Add(entry);

            // Indexer par nom principal
            AddToIndex(_nameIndex, entry.Name, entry);

            // Indexer par noms alternatifs
            foreach (var altName in entry.AlternativeNames)
            {
                AddToIndex(_nameIndex, altName, entry);
            }

            // Indexer par publisher
            if (!string.IsNullOrEmpty(entry.Publisher))
            {
                AddToIndex(_publisherIndex, entry.Publisher, entry);
            }

            // Indexer par mots-clés
            foreach (var keyword in entry.Keywords)
            {
                AddToIndex(_keywordIndex, keyword, entry);
            }

            // Indexer par mots extraits du nom
            var words = ExtractWords(entry.Name);
            foreach (var word in words)
            {
                AddToIndex(_keywordIndex, word, entry);
            }
        }

        private void AddToIndex(Dictionary<string, HashSet<SoftwareEntry>> index, string key, SoftwareEntry entry)
        {
            if (!index.TryGetValue(key, out var set))
            {
                set = new HashSet<SoftwareEntry>();
                index[key] = set;
            }
            set.Add(entry);
        }

        private List<string> ExtractWords(string text)
        {
            return text.ToLowerInvariant()
                      .Split(new[] { ' ', '-', '_', '.', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries)
                      .Where(w => w.Length > 2)
                      .ToList();
        }
    }

    /// <summary>
    /// Entrée dans la base de données de logiciels
    /// </summary>


    /// <summary>
    /// Informations sur le fichier de téléchargement
    /// </summary>

}
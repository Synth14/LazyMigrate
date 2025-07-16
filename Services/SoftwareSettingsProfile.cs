using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LazyMigrate.Models
{
    public class SoftwareSettingsProfile
    {
        public string SoftwareName { get; set; } = string.Empty;
        public List<string> AlternativeNames { get; set; } = new List<string>();
        public List<string> PublisherNames { get; set; } = new List<string>();
        public List<string> ExecutableNames { get; set; } = new List<string>();
        public List<SettingsPath> ConfigPaths { get; set; } = new List<SettingsPath>();
        public List<string> ExcludePatterns { get; set; } = new List<string>();
        public RestoreStrategy Strategy { get; set; } = RestoreStrategy.OverwriteExisting;
        public bool RequiresElevation { get; set; } = false;
        public bool BackupBeforeRestore { get; set; } = true;
        public string Notes { get; set; } = string.Empty;
        public int MatchPriority { get; set; } = 1;

        public bool MatchesSoftware(string softwareName, string publisher = "")
        {
            var score = CalculateMatchScore(softwareName, publisher);
            return score >= 50;
        }

        public int CalculateMatchScore(string softwareName, string publisher = "")
        {
            var score = 0;
            var normalizedSoftwareName = NormalizeName(softwareName);
            var normalizedPublisher = NormalizeName(publisher);

            // 1. Correspondance exacte du nom principal
            if (string.Equals(NormalizeName(SoftwareName), normalizedSoftwareName, StringComparison.OrdinalIgnoreCase))
            {
                score += 100;
            }

            // 2. Correspondance exacte avec un nom alternatif
            foreach (var altName in AlternativeNames)
            {
                if (string.Equals(NormalizeName(altName), normalizedSoftwareName, StringComparison.OrdinalIgnoreCase))
                {
                    score += 90;
                    break;
                }
            }

            // 3. Le nom du logiciel contient le nom du profil (ou vice versa)
            var profileMainName = NormalizeName(SoftwareName);
            if (normalizedSoftwareName.Contains(profileMainName) || profileMainName.Contains(normalizedSoftwareName))
            {
                score += 70;
            }

            // 4. Correspondance avec un nom alternatif (sous-chaîne)
            foreach (var altName in AlternativeNames)
            {
                var normalizedAlt = NormalizeName(altName);
                if (normalizedSoftwareName.Contains(normalizedAlt) || normalizedAlt.Contains(normalizedSoftwareName))
                {
                    score += 60;
                    break;
                }
            }

            // 5. Correspondance par éditeur
            if (!string.IsNullOrEmpty(normalizedPublisher))
            {
                foreach (var pubName in PublisherNames)
                {
                    if (normalizedPublisher.Contains(NormalizeName(pubName), StringComparison.OrdinalIgnoreCase))
                    {
                        score += 30;
                        break;
                    }
                }
            }

            // 6. Correspondance par mots-clés
            score += CalculateKeywordMatch(normalizedSoftwareName);

            // 7. Pénalité pour les noms trop génériques
            if (IsGenericName(normalizedSoftwareName))
            {
                score -= 20;
            }

            return Math.Max(0, score);
        }

        private string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";

            // Supprimer les parenthèses et leur contenu
            name = Regex.Replace(name, @"\([^)]*\)", "").Trim();

            // Supprimer les suffixes courants
            var suffixesToRemove = new[] { " (User)", " (x64)", " (x86)", " (64-bit)", " (32-bit)",
                                          " Setup", " Installer", " Application", " App" };

            foreach (var suffix in suffixesToRemove)
            {
                if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(0, name.Length - suffix.Length).Trim();
                }
            }

            // Supprimer les versions
            name = Regex.Replace(name, @"\s+\d+(\.\d+)*(\.\d+)*(\.\d+)*$", "").Trim();

            // Supprimer les espaces multiples et normaliser
            name = Regex.Replace(name, @"\s+", " ").Trim();

            return name;
        }

        private int CalculateKeywordMatch(string softwareName)
        {
            var score = 0;
            var keywords = ExtractKeywords(NormalizeName(SoftwareName));
            var softwareKeywords = ExtractKeywords(softwareName);

            foreach (var keyword in keywords)
            {
                if (softwareKeywords.Contains(keyword, StringComparer.OrdinalIgnoreCase))
                {
                    score += 15;
                }
            }

            return Math.Min(score, 45); // Max 3 mots-clés
        }

        private List<string> ExtractKeywords(string name)
        {
            return name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                      .Where(word => word.Length > 2)
                      .ToList();
        }

        private bool IsGenericName(string name)
        {
            var genericWords = new[] { "application", "program", "software", "tool", "utility", "launcher" };
            return genericWords.Any(generic => name.Contains(generic, StringComparison.OrdinalIgnoreCase));
        }
    }
}
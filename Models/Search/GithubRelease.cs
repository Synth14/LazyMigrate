namespace LazyMigrate.Models.Search
{
    public class GitHubRelease
    {
        public string tag_name { get; set; } = "";
        public GitHubAsset[]? assets { get; set; }
    }

}

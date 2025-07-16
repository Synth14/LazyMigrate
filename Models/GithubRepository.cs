namespace LazyMigrate.Models
{
    public class GitHubRepository
    {
        public string name { get; set; } = "";
        public string full_name { get; set; } = "";
        public string? description { get; set; }
        public int stargazers_count { get; set; }
    }

}

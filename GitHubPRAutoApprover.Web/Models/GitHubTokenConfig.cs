namespace GitHubPRAutoApprover.Web.Models
{
    public class GitHubTokenConfig
    {
        public string DisplayName { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
    }

    public class GitHubConfig
    {
        public List<GitHubTokenConfig> Tokens { get; set; } = new();
    }
}
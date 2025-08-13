using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Text.Json;

namespace GitHubPRAutoApprover.Web
{
    public class GitHubService
    {
        private const string DefaultApprovalMessage = "Approved by léng zái.";
        private const string GitHubApiVersion = "2022-11-28";
        private const int RequestTimeoutSeconds = 30;

        public bool ValidateGitHubToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length < 10)
                return false;

            string[] validPrefixes = {
                "github_pat_", "ghp_", "gho_", "ghu_", "ghs_", "ghr_"
            };
            foreach (var prefix in validPrefixes)
            {
                if (token.StartsWith(prefix))
                    return true;
            }
            return false;
        }

        public (string owner, string repo, string prNumber)? ParsePrUrl(string prUrl)
        {
            var pattern = @"https://github\.com/([^/]+)/([^/]+)/pull/(\d+)(/files)?";
            var match = Regex.Match(prUrl, pattern);
            if (!match.Success)
                return null;
            return (match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value);
        }

        public async Task<JsonElement?> CheckPrExistsAsync(string owner, string repo, string prNumber, string token)
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(RequestTimeoutSeconds) };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", GitHubApiVersion);
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("D", "1.0"));
            var prCheckUrl = $"https://api.github.com/repos/{owner}/{repo}/pulls/{prNumber}";
            var response = await client.GetAsync(prCheckUrl);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(json).RootElement;
        }

        public async Task<(bool success, string log)> ApprovePrAsync(string prUrl, string githubToken, string approvalMessage = null)
        {
            var log = new StringBuilder();
            approvalMessage ??= DefaultApprovalMessage;

            if (!ValidateGitHubToken(githubToken))
            {
                log.AppendLine("❌ Invalid GitHub token format");
                return (false, log.ToString());
            }

            var parsed = ParsePrUrl(prUrl);
            if (parsed == null)
            {
                log.AppendLine("❌ Invalid PR URL format");
                return (false, log.ToString());
            }
            var (owner, repo, prNumber) = parsed.Value;
            log.AppendLine($"📋 Parsed PR: {owner}/{repo}#{prNumber}");

            var prData = await CheckPrExistsAsync(owner, repo, prNumber, githubToken);
            if (prData == null)
            {
                log.AppendLine("❌ PR not found or access denied.");
                return (false, log.ToString());
            }

            var state = prData.Value.GetProperty("state").GetString();
            var title = prData.Value.GetProperty("title").GetString();
            log.AppendLine($"✅ PR found: '{title}'");
            log.AppendLine($"📊 PR state: {state}");

            if (state != "open")
            {
                log.AppendLine($"❌ Cannot approve a {state} PR");
                return (false, log.ToString());
            }

            if (prData.Value.TryGetProperty("user", out var user) && user.TryGetProperty("login", out var login))
            {
                log.AppendLine($"👤 PR author: {login.GetString()}");
            }

            // Submit approval
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(RequestTimeoutSeconds) };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", GitHubApiVersion);
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(new ProductHeaderValue("D")));
            var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/pulls/{prNumber}/reviews";
            var data = new
            {
                @event = "APPROVE",
                body = approvalMessage
            };
            var jsonData = JsonSerializer.Serialize(data);
            var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

            log.AppendLine("🚀 Attempting to approve PR...");
            var response = await client.PostAsync(apiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                log.AppendLine("✅ PR approved successfully!");
                return (true, log.ToString());
            }
            else
            {
                log.AppendLine($"❌ Failed to approve PR: {(int)response.StatusCode}");
                var respText = await response.Content.ReadAsStringAsync();
                log.AppendLine($"📄 Response: {respText}");

                if ((int)response.StatusCode == 422)
                    log.AppendLine("💡 Tip: You might be trying to approve your own PR, which is not allowed");
                else if ((int)response.StatusCode == 403)
                    log.AppendLine("💡 Tip: Check if your GitHub token has 'repo' or 'public_repo' permissions");
                else if ((int)response.StatusCode == 401)
                    log.AppendLine("💡 Tip: Your GitHub token might be invalid or expired");

                return (false, log.ToString());
            }
        }
    }
}
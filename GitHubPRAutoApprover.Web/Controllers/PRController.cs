using System.Text;
using GitHubPRAutoApprover.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GitHubPRAutoApprover.Web.Controllers
{
    public class PRController : Controller
    {
        private readonly GitHubService _gitHubService;
        private readonly GitHubConfig _gitHubConfig;
        private readonly ILogger<PRController> _logger;

        public PRController(GitHubService gitHubService, IOptions<GitHubConfig> gitHubConfig, ILogger<PRController> logger)
        {
            _gitHubService = gitHubService;
            _gitHubConfig = gitHubConfig.Value;
            _logger = logger;
        }

        public IActionResult Index()
        {
            // Check if user is logged in
            if (HttpContext.Session.GetString("IsLoggedIn") != "true")
            {
                var returnUrl = "/PR/Index";
                return RedirectToAction("Login", "Account", new { returnUrl = returnUrl });
            }

            ViewBag.AvailableTokens = _gitHubConfig.Tokens;
            ViewBag.Username = HttpContext.Session.GetString("Username");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ApprovePRs(string prUrls, string selectedDisplayName)
        {
            // Generate unique request ID for this batch
            var requestId = Guid.NewGuid().ToString("N")[..8]; // Short 8-character ID
            
            // Check if user is logged in
            if (HttpContext.Session.GetString("IsLoggedIn") != "true")
            {
                return RedirectToAction(
                    "Login",
                    "Account",
                    new { sessionExpired = true, returnUrl = "/PR/Index" }
                );
            }

            var username = HttpContext.Session.GetString("Username");
            var log = new StringBuilder();
            var urls =
                prUrls
                    ?.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(u => u.Contains("github.com") && u.Contains("/pull/"))
                    .ToList() ?? new List<string>();

            int total = urls.Count,
                success = 0;
            var failed = new List<string>();

            // Find the selected token
            var tokenConfig = _gitHubConfig.Tokens.FirstOrDefault(t =>
                t.DisplayName == selectedDisplayName
            );
            if (tokenConfig == null)
            {
                log.AppendLine("❌ No valid GitHub access token selected!");
                ViewBag.StatusLog = log.ToString();
                ViewBag.AvailableTokens = _gitHubConfig.Tokens;
                return View("Index");
            }

            // Write audit log header
            await WriteAuditLogAsync(requestId, $"PR_APPROVAL_BATCH_START", username, selectedDisplayName, total);

            log.AppendLine(
                $"🚀 Starting batch processing of {total} PR(s) using token: {tokenConfig.DisplayName}... [RequestID: {requestId}]"
            );

            foreach (var (url, i) in urls.Select((u, idx) => (u, idx + 1)))
            {
                log.AppendLine($"\n📋 [{i}/{total}] Processing: {url}");
                try
                {
                    await _gitHubService.ApprovePrAsync(url, tokenConfig.AccessToken);
                    log.AppendLine(
                        $"✅ [{i}/{total}] Approval completed by {tokenConfig.DisplayName}"
                    );
                    success++;
                    
                    // Write individual approval log
                    await WriteAuditLogAsync(requestId, $"PR_APPROVAL_SUCCESS", username, selectedDisplayName, 1, url);
                }
                catch (Exception ex)
                {
                    failed.Add(url);
                    log.AppendLine($"❌ [{i}/{total}] Error: {ex.Message}");
                    
                    // Write individual failure log
                    await WriteAuditLogAsync(requestId, $"PR_APPROVAL_FAILED", username, selectedDisplayName, 0, url, ex.Message);
                }
            }

            // Write audit log summary
            await WriteAuditLogAsync(requestId, $"PR_APPROVAL_BATCH_END", username, selectedDisplayName, success, 
                $"Total: {total}, Success: {success}, Failed: {failed.Count}");

            log.AppendLine($"\n{'=' * 50}\n📊 Batch Processing Summary:");
            log.AppendLine($"   Request ID: {requestId}");
            log.AppendLine($"   Token Used: {tokenConfig.DisplayName}");
            log.AppendLine($"   Total PRs: {total}");
            log.AppendLine($"   Successful: {success}");
            log.AppendLine($"   Failed: {failed.Count}");
            if (failed.Count > 0)
            {
                log.AppendLine("\n❌ Failed URLs:");
                foreach (var url in failed)
                    log.AppendLine($"   - {url}");
            }

            ViewBag.StatusLog = log.ToString();
            ViewBag.AvailableTokens = _gitHubConfig.Tokens;
            return View("Index");
        }

        private async Task WriteAuditLogAsync(string requestId, string action, string username, string tokenDisplayName, 
            int count, string prUrl = null, string errorMessage = null)
        {
            try
            {
                var logDir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "Audit");
                Directory.CreateDirectory(logDir);
                
                var logFile = Path.Combine(logDir, $"pr-approvals-{DateTime.Now:yyyy-MM-dd}.log");
                
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] " +
                              $"REQUEST_ID: {requestId} | " +
                              $"ACTION: {action} | " +
                              $"USER: {username} | " +
                              $"TOKEN: {tokenDisplayName} | " +
                              $"COUNT: {count}";
                
                if (!string.IsNullOrEmpty(prUrl))
                    logEntry += $" | PR_URL: {prUrl}";
                
                if (!string.IsNullOrEmpty(errorMessage))
                    logEntry += $" | ERROR: {errorMessage}";
                
                logEntry += Environment.NewLine;
                
                await System.IO.File.AppendAllTextAsync(logFile, logEntry);
                
                // Also log to application logger with request ID
                _logger.LogInformation("PR Approval Audit [RequestID: {RequestId}]: {Action} by {Username} using {TokenDisplayName}, Count: {Count}", 
                    requestId, action, username, tokenDisplayName, count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write audit log for PR approval action: {Action} [RequestID: {RequestId}]", action, requestId);
            }
        }
    }
}

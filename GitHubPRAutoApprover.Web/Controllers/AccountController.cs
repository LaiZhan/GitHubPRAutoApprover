using System.Runtime.InteropServices;
using GitHubPRAutoApprover.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GitHubPRAutoApprover.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly GitHubConfig _gitHubConfig;

        public AccountController(IOptions<GitHubConfig> gitHubConfig)
        {
            _gitHubConfig = gitHubConfig.Value;
        }

        [HttpGet] // 明确指定这是GET方法
        public IActionResult Login(bool sessionExpired = false, string returnUrl = null)
        {
            // If user is already logged in, redirect to target page
            if (HttpContext.Session.GetString("IsLoggedIn") == "true")
            {
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction("Index", "PR");
            }

            if (sessionExpired)
            {
                ViewBag.SessionExpired = true;
            }

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost] // 明确指定这是POST方法
        public IActionResult Login(string username, string password, string returnUrl = null)
        {
            Console.WriteLine($"username: {username}, password: {password}, returnUrl: {returnUrl}");
            // Validate username (must not be empty)
            if (string.IsNullOrWhiteSpace(username))
            {
                ViewBag.ErrorMessage = "Username cannot be empty.";
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }

            // Find the matching token by DisplayName
            var matchingToken = _gitHubConfig.Tokens.FirstOrDefault(x => 
                string.Equals(x.DisplayName, username, StringComparison.OrdinalIgnoreCase));

            if (matchingToken == null)
            {   
                ViewBag.ErrorMessage = "Invalid username.";
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }

            // Validate password format (yyyyMMddHHmm)
            if (!IsValidPasswordFormat(password))
            {
                ViewBag.ErrorMessage = "Invalid password format.";
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }

            // Check if password matches current time format
            string tzId = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "China Standard Time"
                : "Asia/Shanghai";

            var shanghaiTimeZone = TimeZoneInfo.FindSystemTimeZoneById(tzId);
            DateTime currentTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, shanghaiTimeZone);
            var expectedPassword = currentTime.ToString("yyyyMMddHHmm");

            // Allow some tolerance (±2 minutes)
            var validPasswords = new List<string>();
            for (int i = -2; i <= 2; i++)
            {
                var timeVariant = currentTime.AddMinutes(i);
                validPasswords.Add(timeVariant.ToString("yyyyMMddHHmm"));
            }

            foreach (var validPassword in validPasswords)
            {
                Console.WriteLine(validPassword);
            }
            
            if (validPasswords.Contains(password))
            {
                // Set session with the actual DisplayName from config
                HttpContext.Session.SetString("IsLoggedIn", "true");
                HttpContext.Session.SetString("Username", matchingToken.DisplayName);

                // Redirect to return URL or default to PR page
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                return RedirectToAction("Index", "PR");
            }
            else
            {
                ViewBag.ErrorMessage = "Invalid password.";
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        private bool IsValidPasswordFormat(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length != 12)
                return false;

            return DateTime.TryParseExact(
                password,
                "yyyyMMddHHmm",
                null,
                System.Globalization.DateTimeStyles.None,
                out _
            );
        }
    }
}

using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Domain.Entities;
using Infra.Context;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace jff_agent_runner_web.Pages.Account;

public class LoginModel(JffAgentDbContext db, IPasswordHasher<Configuration> hasher) : PageModel
{
    [BindProperty] public LoginInput Input { get; set; } = new();
    public async Task<IActionResult> OnGetAsync()
    {
        if (!await db.Configurations.AnyAsync()) return RedirectToPage("/Account/Setup");
        if (User.Identity?.IsAuthenticated == true) return Redirect("/");
        return Page();
    }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var config = await db.Configurations.FirstOrDefaultAsync(c => c.Username == Input.Username.Trim());
        if (config is null || hasher.VerifyHashedPassword(config, config.PasswordHash, Input.Password) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return Page();
        }
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, config.Username)], CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return Redirect("/");
    }
    public class LoginInput
    {
        [Required] public string Username { get; set; } = "";
        [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
    }
}

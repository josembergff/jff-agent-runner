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

public class SetupModel(JffAgentDbContext db, IPasswordHasher<Configuration> hasher) : PageModel
{
    [BindProperty] public SetupInput Input { get; set; } = new();
    public async Task<IActionResult> OnGetAsync() => await db.Configurations.AnyAsync() ? RedirectToPage("/Account/Login") : Page();
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        if (await db.Configurations.AnyAsync()) return RedirectToPage("/Account/Login");
        var config = new Configuration { Id = 1, Username = Input.Username.Trim() };
        config.PasswordHash = hasher.HashPassword(config, Input.Password);
        db.Configurations.Add(config);
        await db.SaveChangesAsync();
        await SignInAsync(config.Username);
        return Redirect("/");
    }
    private async Task SignInAsync(string username) => await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, username)], CookieAuthenticationDefaults.AuthenticationScheme)));
    public class SetupInput
    {
        [Required, StringLength(80, MinimumLength = 3)] public string Username { get; set; } = "";
        [Required, DataType(DataType.Password), MinLength(8)] public string Password { get; set; } = "";
        [Required, Compare(nameof(Password))] public string ConfirmPassword { get; set; } = "";
    }
}

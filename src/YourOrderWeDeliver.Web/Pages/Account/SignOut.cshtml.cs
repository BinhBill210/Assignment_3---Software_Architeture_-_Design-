using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace YourOrderWeDeliver.Web.Pages.Account;

/// <summary>Signs the user out.</summary>
public sealed class SignOutModel : PageModel
{
    /// <summary>Redirects direct visits to the home page.</summary>
    public IActionResult OnGet() => RedirectToPage("/Index");

    /// <summary>Clears the authentication cookie.</summary>
    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Index");
    }
}

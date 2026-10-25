using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using YourOrderWeDeliver.Web.Infrastructure;

namespace YourOrderWeDeliver.Web.Pages.Account;

/// <summary>Staff sign-in (A1 QR3.4: users authenticate before allocation, availability, statistics and staff records).</summary>
public sealed class SignInModel(StaffAccountService accounts) : PageModel
{
    /// <summary>Gets or sets the submitted credentials.</summary>
    [BindProperty]
    public SignInInput Input { get; set; } = new();

    /// <summary>Gets or sets the page to return to after signing in.</summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    /// <summary>Shows the empty sign-in form.</summary>
    public void OnGet()
    {
    }

    /// <summary>Checks the credentials and signs the user in.</summary>
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        ClaimsPrincipal? principal = accounts.Authenticate(Input.Username, Input.Password);
        if (principal is null)
        {
            ModelState.AddModelError(string.Empty, "The username or password is incorrect, or the staff record is inactive. Check your details and try again.");
            return Page();
        }

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        return Url.IsLocalUrl(ReturnUrl) ? LocalRedirect(ReturnUrl) : RedirectToPage("/Index");
    }

    /// <summary>Sign-in form fields.</summary>
    public sealed class SignInInput
    {
        /// <summary>Gets or sets the username or delivery staff ID.</summary>
        [Required(ErrorMessage = "Enter your username or staff ID.")]
        [Display(Name = "Username or staff ID")]
        public string? Username { get; set; }

        /// <summary>Gets or sets the password.</summary>
        [Required(ErrorMessage = "Enter your password.")]
        [DataType(DataType.Password)]
        public string? Password { get; set; }
    }
}

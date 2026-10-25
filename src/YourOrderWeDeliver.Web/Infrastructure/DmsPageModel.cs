using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using YourOrderWeDeliver.Core.Common;
using YourOrderWeDeliver.Core.Coordination;

namespace YourOrderWeDeliver.Web.Infrastructure;

/// <summary>
/// Base class for pages. Pages are thin: they translate form input into calls on the domain
/// objects, save after a successful change, and show business-rule failures next to the form.
/// </summary>
public abstract class DmsPageModel(DeliveryManagementSystem system) : PageModel
{
    /// <summary>Gets or sets a one-off confirmation shown at the top of the next page.</summary>
    [TempData]
    public string? StatusMessage { get; set; }

    /// <summary>Gets the single system instance.</summary>
    protected DeliveryManagementSystem System { get; } = system;

    /// <summary>Gets the delivery register.</summary>
    protected DeliveryRegister Register => System.Register;

    /// <summary>Gets the staff roster.</summary>
    protected StaffRoster Roster => System.Roster;

    /// <summary>Gets the current time from the system clock.</summary>
    protected DateTime Now => System.Clock.Now;

    /// <summary>
    /// Runs a change and saves it. A <see cref="BusinessRuleException"/> is shown on the page instead,
    /// and nothing is saved.
    /// </summary>
    /// <returns><c>true</c> when the change succeeded.</returns>
    protected bool TryChange(Action change, string? successMessage = null)
    {
        ArgumentNullException.ThrowIfNull(change);
        try
        {
            change();
            System.SaveChanges();
            if (successMessage is not null)
            {
                StatusMessage = successMessage;
            }

            return true;
        }
        catch (BusinessRuleException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return false;
        }
    }

    /// <summary>Shows a "not found" message on the page.</summary>
    protected IActionResult NotFoundMessage(string message)
    {
        StatusMessage = null;
        ViewData["NotFound"] = message;
        return Page();
    }
}

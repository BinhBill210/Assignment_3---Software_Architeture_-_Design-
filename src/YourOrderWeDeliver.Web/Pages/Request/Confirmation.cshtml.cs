using Microsoft.AspNetCore.Mvc;
using YourOrderWeDeliver.Core.Coordination;
using YourOrderWeDeliver.Core.Domain;
using YourOrderWeDeliver.Web.Infrastructure;

namespace YourOrderWeDeliver.Web.Pages.Request;

/// <summary>A1 T1 subtask 6: confirms the request and gives the customer their reference.</summary>
public sealed class ConfirmationModel(DeliveryManagementSystem system) : DmsPageModel(system)
{
    /// <summary>Gets the request just recorded.</summary>
    public DeliveryRequest? DeliveryRequest { get; private set; }

    /// <summary>Loads the request recorded in this browser session.</summary>
    public IActionResult OnGet()
    {
        DeliveryRequest = Register.FindRequest(TempData["NewReference"] as string);
        return DeliveryRequest is null ? RedirectToPage("New") : Page();
    }
}

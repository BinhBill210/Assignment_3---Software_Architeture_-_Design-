using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using YourOrderWeDeliver.Core.Coordination;
using YourOrderWeDeliver.Core.Domain;
using YourOrderWeDeliver.Web.Infrastructure;

namespace YourOrderWeDeliver.Web.Pages.Track;

/// <summary>
/// A1 task T8: track a delivery. Customers must give the reference and their email or phone
/// (variant 1a, QR3.3); signed-in staff only need the request reference or delivery number (1b, 1c).
/// </summary>
public sealed class IndexModel(DeliveryManagementSystem system) : DmsPageModel(system)
{
    /// <summary>Gets or sets the request reference or delivery number.</summary>
    [BindProperty(SupportsGet = true)]
    [Required(ErrorMessage = "Enter a request reference (REQ-…) or delivery number (DEL-…).")]
    [StringLength(30)]
    public string? Reference { get; set; }

    /// <summary>Gets or sets the customer's email or phone.</summary>
    [BindProperty]
    [StringLength(254)]
    public string? Contact { get; set; }

    /// <summary>Gets the request found.</summary>
    public DeliveryRequest? DeliveryRequest { get; private set; }

    /// <summary>Gets whether the viewer is staff (sees full details).</summary>
    public bool IsStaffView => User.Identity?.IsAuthenticated ?? false;

    /// <summary>Shows the form; staff following a link see the result straight away.</summary>
    public void OnGet()
    {
        ModelState.Clear();
        if (IsStaffView && !string.IsNullOrWhiteSpace(Reference))
        {
            DeliveryRequest = FindForStaff(Reference);
        }
    }

    /// <summary>Looks up the delivery.</summary>
    public IActionResult OnPost()
    {
        if (!IsStaffView && string.IsNullOrWhiteSpace(Contact))
        {
            ModelState.AddModelError(nameof(Contact), "Enter the email address or phone number used on the request.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        DeliveryRequest = IsStaffView ? FindForStaff(Reference) : FindForCustomer(Reference, Contact);
        if (DeliveryRequest is null)
        {
            ModelState.AddModelError(
                string.Empty,
                IsStaffView
                    ? $"No request or delivery matches '{Reference}'. Check the number and try again."
                    : "We could not find a delivery with that reference and contact detail. Check both and try again, or call our office.");
        }

        return Page();
    }

    private DeliveryRequest? FindForStaff(string? reference)
    {
        return Register.FindRequest(reference) ?? Register.FindDelivery(reference)?.Request;
    }

    private DeliveryRequest? FindForCustomer(string? reference, string? contact)
    {
        DeliveryRequest? request = FindForStaff(reference);
        return request is not null && request.Customer.IsIdentifiedBy(contact) ? request : null;
    }
}

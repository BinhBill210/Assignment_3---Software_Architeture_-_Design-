using Microsoft.AspNetCore.Mvc;
using YourOrderWeDeliver.Core.Common;
using YourOrderWeDeliver.Core.Coordination;
using YourOrderWeDeliver.Core.Domain;
using YourOrderWeDeliver.Web.Infrastructure;
using YourOrderWeDeliver.Web.Models;

namespace YourOrderWeDeliver.Web.Pages.Request;

/// <summary>
/// A1 task T1: request a delivery. Used by customers (variant 1a) and by office staff on a
/// customer's behalf (variant 1b). The request is reviewed before it is submitted (subtask 4).
/// </summary>
public sealed class NewModel(DeliveryManagementSystem system) : DmsPageModel(system)
{
    /// <summary>Gets or sets the form fields.</summary>
    [BindProperty]
    public RequestInput Input { get; set; } = new();

    /// <summary>Gets whether the page shows the review step.</summary>
    public bool IsReviewing { get; private set; }

    /// <summary>Gets the service area name shown as guidance.</summary>
    public string ServiceAreaName => Register.ServiceArea.Name;

    /// <summary>Shows an empty form with one parcel row.</summary>
    public void OnGet() => Input = RequestInput.CreateEmpty(DateOnly.FromDateTime(Now));

    /// <summary>Adds a parcel row without validating the rest of the form.</summary>
    public IActionResult OnPostAddParcel()
    {
        ModelState.Clear();
        if (Input.Parcels.Count >= DeliveryRequest.MaxParcels)
        {
            ModelState.AddModelError(string.Empty, $"A single request can contain at most {DeliveryRequest.MaxParcels} parcels. Submit a second request for the rest.");
        }
        else
        {
            Input.Parcels.Add(new ParcelInput());
        }

        return Page();
    }

    /// <summary>Removes a parcel row (the customer changed their mind).</summary>
    public IActionResult OnPostRemoveParcel(int index)
    {
        ModelState.Clear();
        if (Input.Parcels.Count <= 1)
        {
            ModelState.AddModelError(string.Empty, "A request needs at least one parcel, so the last parcel cannot be removed.");
        }
        else if (index >= 0 && index < Input.Parcels.Count)
        {
            Input.Parcels.RemoveAt(index);
        }

        return Page();
    }

    /// <summary>Validates every field and shows the review step.</summary>
    public IActionResult OnPostReview()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            Register.ValidateRequest(Input.ToDetails(), Now);
        }
        catch (BusinessRuleException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }

        IsReviewing = true;
        return Page();
    }

    /// <summary>Returns from the review step to the editable form.</summary>
    public IActionResult OnPostEdit()
    {
        ModelState.Clear();
        return Page();
    }

    /// <summary>Records the request (subtask 5) and shows the reference (subtask 6).</summary>
    public IActionResult OnPostSubmit()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        RequestChannel channel = User.IsOffice() ? RequestChannel.OfficeStaff : RequestChannel.CustomerOnline;
        DeliveryRequest? request = null;
        if (!TryChange(() => request = Register.RecordRequest(Input.ToDetails(), channel, Now)))
        {
            return Page();
        }

        if (User.IsOffice())
        {
            StatusMessage = $"Request {request!.Reference} recorded for {request.Customer.Name}. You can prepare the quote now.";
            return RedirectToPage("/Office/Requests/Details", new { reference = request.Reference });
        }

        TempData["NewReference"] = request!.Reference;
        return RedirectToPage("Confirmation");
    }
}

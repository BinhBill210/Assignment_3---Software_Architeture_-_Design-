using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using YourOrderWeDeliver.Core.Common;
using YourOrderWeDeliver.Core.Coordination;
using YourOrderWeDeliver.Core.Domain;
using YourOrderWeDeliver.Web.Infrastructure;

namespace YourOrderWeDeliver.Web.Pages.Driver;

/// <summary>
/// A1 tasks T6 and T7 (A2 Scenario 3): the allocated staff member collects each parcel, records
/// pickup with the payment received, delivers each parcel and records completion. Marks entered
/// by mistake can be undone until the next stage is recorded.
/// </summary>
public sealed class DeliveryModel(DeliveryManagementSystem system) : DmsPageModel(system)
{
    /// <summary>Gets or sets the delivery number from the route.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Id { get; set; }

    /// <summary>Gets or sets the payment method chosen at pickup.</summary>
    [BindProperty]
    [Required(ErrorMessage = "Choose how the customer paid.")]
    [Display(Name = "Payment method")]
    public PaymentMethod? Method { get; set; }

    /// <summary>Gets or sets the name of the person receiving the parcels.</summary>
    [BindProperty]
    [Required(ErrorMessage = "Enter the name of the person who received the parcels.")]
    [StringLength(100, ErrorMessage = "The name must be 100 characters or fewer.")]
    [Display(Name = "Received by")]
    public string? RecipientName { get; set; }

    /// <summary>Gets or sets the issue being reported.</summary>
    [BindProperty]
    [Required(ErrorMessage = "Describe the problem.")]
    [StringLength(300, ErrorMessage = "The description must be 300 characters or fewer.")]
    [Display(Name = "Issue")]
    public string? IssueText { get; set; }

    /// <summary>Gets the delivery being worked on.</summary>
    public Delivery? Delivery { get; private set; }

    /// <summary>Gets the signed-in staff member.</summary>
    public DeliveryStaff? Me { get; private set; }

    /// <summary>Gets the payment recorded in this request, shown as a receipt.</summary>
    public Payment? Receipt { get; private set; }

    /// <summary>Shows the delivery.</summary>
    public IActionResult OnGet()
    {
        ModelState.Clear();
        return Load() ? Page() : NotFoundMessage($"Delivery '{Id}' is not allocated to you.");
    }

    /// <summary>Marks one parcel as collected.</summary>
    public IActionResult OnPostCollect(string parcelId) =>
        Change(() => Delivery!.RecordParcelCollected(parcelId, Me!, Now), $"Parcel {parcelId} marked as collected.");

    /// <summary>Undoes a collection mark.</summary>
    public IActionResult OnPostUndoCollect(string parcelId) =>
        Change(() => Delivery!.UndoParcelCollected(parcelId, Me!, Now), $"Parcel {parcelId} is no longer marked as collected.");

    /// <summary>Records pickup and the payment received (A1 T6; no real payment is processed).</summary>
    public IActionResult OnPostPickup()
    {
        KeepValidationFor(nameof(Method));
        if (!Load())
        {
            return NotFoundMessage($"Delivery '{Id}' is not allocated to you.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        Payment? payment = null;
        if (!TryChange(() => payment = Delivery!.RecordPickup(Method!.Value, Me!, Now)))
        {
            return Page();
        }

        StatusMessage = $"Pickup recorded. Payment of {DisplayFormat.Money(payment!.Amount)} by {DisplayText.Label(payment.Method)} "
            + $"has been processed (receipt {payment.Id}). Now deliver the parcels to {Delivery!.Request.DestinationAddress}.";
        return RedirectToPage(new { id = Id });
    }

    /// <summary>Marks one parcel as delivered.</summary>
    public IActionResult OnPostDeliver(string parcelId) =>
        Change(() => Delivery!.RecordParcelDelivered(parcelId, Me!, Now), $"Parcel {parcelId} marked as delivered.");

    /// <summary>Undoes a delivery mark.</summary>
    public IActionResult OnPostUndoDeliver(string parcelId) =>
        Change(() => Delivery!.UndoParcelDelivered(parcelId, Me!, Now), $"Parcel {parcelId} is no longer marked as delivered.");

    /// <summary>Records completion (A1 T7).</summary>
    public IActionResult OnPostComplete()
    {
        KeepValidationFor(nameof(RecipientName));
        if (!Load())
        {
            return NotFoundMessage($"Delivery '{Id}' is not allocated to you.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!TryChange(() => Delivery!.RecordCompletion(RecipientName, Me!, Now)))
        {
            return Page();
        }

        StatusMessage = $"Delivery {Id} completed. Thank you!";
        return RedirectToPage("Index");
    }

    /// <summary>Records a problem so the office can see it when tracking.</summary>
    public IActionResult OnPostReportIssue()
    {
        KeepValidationFor(nameof(IssueText));
        if (!Load())
        {
            return NotFoundMessage($"Delivery '{Id}' is not allocated to you.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        bool saved = TryChange(() => Delivery!.RecordIssue(IssueText, Me!.Name, Now), "Issue recorded. The office can see it in the delivery history.");
        return saved ? RedirectToPage(new { id = Id }) : Page();
    }

    private IActionResult Change(Action change, string message)
    {
        ModelState.Clear();
        if (!Load())
        {
            return NotFoundMessage($"Delivery '{Id}' is not allocated to you.");
        }

        return TryChange(change, message) ? RedirectToPage(new { id = Id }) : Page();
    }

    private bool Load()
    {
        Me = Roster.Find(User.StaffId());
        Delivery = Register.FindDelivery(Id);
        if (Me is null || Delivery is null || !ReferenceEquals(Delivery.AllocatedStaff, Me))
        {
            Delivery = null;
            return false;
        }

        Receipt = Delivery.Payment;
        return true;
    }

    private void KeepValidationFor(string key)
    {
        foreach (string other in ModelState.Keys.Where(existing => existing.Length > 0 && existing != key).ToList())
        {
            ModelState.Remove(other);
        }
    }
}

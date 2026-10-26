using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using YourOrderWeDeliver.Core.Coordination;
using YourOrderWeDeliver.Core.Domain;
using YourOrderWeDeliver.Web.Infrastructure;

namespace YourOrderWeDeliver.Web.Pages.Quote;

/// <summary>
/// A1 task T3: the customer reviews the quote and accepts or declines it. The request is found by
/// reference plus email or phone (A1 QR3.3), and the choice is confirmed before it is recorded.
/// </summary>
public sealed class RespondModel(DeliveryManagementSystem system) : DmsPageModel(system)
{
    /// <summary>Gets or sets the lookup fields.</summary>
    [BindProperty]
    public LookupInput Lookup { get; set; } = new();

    /// <summary>Gets the request found, if any.</summary>
    public DeliveryRequest? DeliveryRequest { get; private set; }

    /// <summary>Gets the response waiting for confirmation, if the customer has chosen one.</summary>
    public bool? PendingChoice { get; private set; }

    /// <summary>Gets the delivery created when the customer accepted.</summary>
    public Delivery? CreatedDelivery { get; private set; }

    /// <summary>Gets whether the quote can still be answered.</summary>
    public bool CanRespond => DeliveryRequest?.Quote is { Response: QuoteResponse.Pending } quote && !quote.IsExpired(Now);

    /// <summary>Shows the empty lookup form, optionally pre-filled with a reference.</summary>
    public void OnGet(string? reference) => Lookup.Reference = reference;

    /// <summary>Finds the request for the reference and contact entered.</summary>
    public IActionResult OnPostFind() => FindOrReport() ? Page() : Page();

    /// <summary>Asks the customer to confirm their choice.</summary>
    public IActionResult OnPostChoose(bool accept)
    {
        if (FindOrReport() && CanRespond)
        {
            PendingChoice = accept;
        }

        return Page();
    }

    /// <summary>Returns to the quote without recording anything (change of mind).</summary>
    public IActionResult OnPostCancel()
    {
        FindOrReport();
        return Page();
    }

    /// <summary>Records the confirmed response.</summary>
    public IActionResult OnPostConfirm(bool accept)
    {
        if (!FindOrReport())
        {
            return Page();
        }

        Delivery? delivery = null;
        if (TryChange(() => delivery = Register.RecordQuoteResponse(DeliveryRequest!, accept, Now)))
        {
            CreatedDelivery = delivery;
            ViewData["Outcome"] = accept
                ? "Thank you. Your acceptance has been recorded and the delivery is being scheduled."
                : "Your decision to decline the quote has been recorded. The request will not proceed.";
        }

        return Page();
    }

    private bool FindOrReport()
    {
        if (!ModelState.IsValid)
        {
            return false;
        }

        DeliveryRequest = Register.FindRequestForCustomer(Lookup.Reference, Lookup.Contact);
        if (DeliveryRequest is null)
        {
            ModelState.AddModelError(
                string.Empty,
                "We could not find a request with that reference and contact detail. Check both and try again, or call our office.");
            return false;
        }

        return true;
    }

    /// <summary>Lookup form fields.</summary>
    public sealed class LookupInput
    {
        /// <summary>Gets or sets the request reference.</summary>
        [Required(ErrorMessage = "Enter the reference number you received, for example REQ-100001.")]
        [RegularExpression(@"^\s*[Rr][Ee][Qq]-\d{6}\s*$", ErrorMessage = "The reference looks like REQ- followed by six digits, for example REQ-100001.")]
        [Display(Name = "Reference number")]
        public string? Reference { get; set; }

        /// <summary>Gets or sets the email or phone used on the request.</summary>
        [Required(ErrorMessage = "Enter the email address or phone number used on the request.")]
        [StringLength(254)]
        [Display(Name = "Email or phone number")]
        public string? Contact { get; set; }
    }
}

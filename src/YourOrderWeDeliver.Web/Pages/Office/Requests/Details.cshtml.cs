using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using YourOrderWeDeliver.Core.Common;
using YourOrderWeDeliver.Core.Coordination;
using YourOrderWeDeliver.Core.Domain;
using YourOrderWeDeliver.Web.Infrastructure;
using YourOrderWeDeliver.Web.Models;

namespace YourOrderWeDeliver.Web.Pages.Office.Requests;

/// <summary>
/// A1 task T2: office staff review a request, prepare (or revise) its quote and check it before
/// issuing it. Also lets office staff correct request details before quoting and record a
/// customer's response given by phone (A1 T3 variant 2a).
/// </summary>
public sealed class DetailsModel(DeliveryManagementSystem system) : DmsPageModel(system)
{
    /// <summary>Default length of a pickup-to-delivery window offered as a suggestion.</summary>
    public static readonly TimeSpan SuggestedDuration = TimeSpan.FromHours(3);

    /// <summary>Gets or sets the request reference from the route.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Reference { get; set; }

    /// <summary>Gets or sets the quote form fields.</summary>
    [BindProperty]
    public QuoteInput Quote { get; set; } = new();

    /// <summary>Gets or sets the request-details form fields.</summary>
    [BindProperty]
    public RequestDetailsInput Details { get; set; } = new();

    /// <summary>Gets the request being worked on.</summary>
    public DeliveryRequest? DeliveryRequest { get; private set; }

    /// <summary>Gets the cost suggested by the pricing policy.</summary>
    public decimal SuggestedCost { get; private set; }

    /// <summary>Gets a description of how the suggested cost was calculated.</summary>
    public string PricingDescription => System.PricingPolicy.Description;

    /// <summary>Gets the earliest window in which some staff member is available.</summary>
    public DeliveryWindow? SuggestedWindow { get; private set; }

    /// <summary>Gets the active staff available for the window currently entered, for guidance.</summary>
    public IReadOnlyList<DeliveryStaff> StaffForEnteredWindow { get; private set; } = [];

    /// <summary>Gets whether availability was checked for the entered times.</summary>
    public bool AvailabilityChecked { get; private set; }

    /// <summary>Gets whether the quote review step is shown.</summary>
    public bool IsReviewingQuote { get; private set; }

    /// <summary>Gets whether the request-details edit form is shown.</summary>
    public bool IsEditingDetails { get; private set; }

    /// <summary>Gets the customer response awaiting confirmation, if office staff chose one.</summary>
    public bool? PendingResponse { get; private set; }

    /// <summary>Gets whether the current quote can no longer be accepted.</summary>
    public bool QuoteExpired => DeliveryRequest?.Quote?.IsExpired(Now) ?? false;

    /// <summary>Gets whether a quote may still be prepared or revised.</summary>
    public bool CanQuote => DeliveryRequest?.Status is RequestStatus.Submitted or RequestStatus.Quoted;

    /// <summary>Shows the request with the quote form pre-filled with suggestions.</summary>
    public IActionResult OnGet()
    {
        ModelState.Clear();
        if (!Load())
        {
            return NotFoundMessage($"No request with reference '{Reference}' was found.");
        }

        FillQuoteFromCurrentOrSuggestion();
        return Page();
    }

    /// <summary>Replaces the entered times with the earliest feasible window (A1 T2 variant 3a).</summary>
    public IActionResult OnPostUseSuggestion()
    {
        ModelState.Clear();
        if (!Load())
        {
            return NotFoundMessage($"No request with reference '{Reference}' was found.");
        }

        if (SuggestedWindow is { } window)
        {
            Quote.EstimatedPickup = window.Start;
            Quote.EstimatedDelivery = window.End;
            StaffForEnteredWindow = Roster.FindSuitableStaff(window);
        }
        else
        {
            ModelState.AddModelError(string.Empty, "No staff have availability recorded in the coming days. Update staff availability first.");
        }

        Quote.Cost ??= SuggestedCost;
        return Page();
    }

    /// <summary>Checks staff availability without issuing a quote or reserving staff.</summary>
    public IActionResult OnPostCheckAvailability()
    {
        KeepValidationFor(nameof(Quote));
        ModelState.Remove("Quote.Cost");

        if (!Load())
        {
            return NotFoundMessage($"No request with reference '{Reference}' was found.");
        }

        if (!CanQuote)
        {
            ModelState.AddModelError(
                string.Empty,
                "The customer has already responded, so this quote can no longer be changed.");
            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        DateTime pickup = Quote.EstimatedPickup!.Value;
        DateTime delivery = Quote.EstimatedDelivery!.Value;

        if (pickup <= Now + DeliveryQuote.AcceptanceLeadTime)
        {
            ModelState.AddModelError(
                "Quote.EstimatedPickup",
                "Choose a pickup time more than one hour from now.");
            return Page();
        }

        if (delivery <= pickup)
        {
            ModelState.AddModelError(
                "Quote.EstimatedDelivery",
                "The estimated delivery time must be later than pickup.");
            return Page();
        }

        if (delivery - pickup > DeliveryQuote.MaxDeliveryDuration)
        {
            ModelState.AddModelError(
                "Quote.EstimatedDelivery",
                "The estimated delivery time must be within 12 hours of pickup.");
            return Page();
        }

        StaffForEnteredWindow = Roster.FindSuitableStaff(
            new DeliveryWindow(pickup, delivery));
        AvailabilityChecked = true;
        return Page();
    }


    /// <summary>Checks the quote and shows it for review before it is issued (A1 T2 subtask 5).</summary>
    public IActionResult OnPostReviewQuote()
    {
        KeepValidationFor(nameof(Quote));
        if (!Load())
        {
            return NotFoundMessage($"No request with reference '{Reference}' was found.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            DeliveryRequest!.ValidateQuote(Quote.ToTerms(), Roster, Now);
        }
        catch (BusinessRuleException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }

        StaffForEnteredWindow = Roster.FindSuitableStaff(Quote.ToWindow());
        IsReviewingQuote = true;
        return Page();
    }

    /// <summary>Returns from the review step to the editable quote form.</summary>
    public IActionResult OnPostEditQuote()
    {
        ModelState.Clear();
        return Load() ? Page() : NotFoundMessage($"No request with reference '{Reference}' was found.");
    }

    /// <summary>Records the quote against the request and makes it available to the customer (A1 T2 subtask 6).</summary>
    public IActionResult OnPostIssueQuote()
    {
        KeepValidationFor(nameof(Quote));
        if (!Load())
        {
            return NotFoundMessage($"No request with reference '{Reference}' was found.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        bool revising = DeliveryRequest!.Quote is not null;
        DeliveryQuote? issued = null;
        if (!TryChange(() => issued = DeliveryRequest.PrepareQuote(Quote.ToTerms(), Roster, Now)))
        {
            return Page();
        }

        StatusMessage = revising
            ? $"Quote revised (revision {issued!.Revision}). The customer sees the new cost and times."
            : $"Quote issued: {DisplayFormat.Money(issued!.Cost)}, {DisplayFormat.Window(issued.Window)}. The customer can now respond.";
        return RedirectToPage(new { reference = DeliveryRequest.Reference });
    }

    /// <summary>Shows the request-details edit form, allowed until a quote is prepared.</summary>
    public IActionResult OnPostEditDetails()
    {
        ModelState.Clear();
        if (!Load())
        {
            return NotFoundMessage($"No request with reference '{Reference}' was found.");
        }

        Details = RequestDetailsInput.From(DeliveryRequest!);
        IsEditingDetails = true;
        FillQuoteFromCurrentOrSuggestion();
        return Page();
    }

    /// <summary>Discards changes to the request details.</summary>
    public IActionResult OnPostCancelDetails() => RedirectToPage(new { reference = Reference });

    /// <summary>Saves corrected pickup, destination, date and instructions.</summary>
    public IActionResult OnPostSaveDetails()
    {
        KeepValidationFor(nameof(Details));
        if (!Load())
        {
            return NotFoundMessage($"No request with reference '{Reference}' was found.");
        }

        IsEditingDetails = true;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        bool saved = TryChange(
            () => DeliveryRequest!.UpdateDetails(
                Address.Create(Details.Pickup.Street, Details.Pickup.Suburb, Details.Pickup.Postcode, "Pickup"),
                Address.Create(Details.Destination.Street, Details.Destination.Suburb, Details.Destination.Postcode, "Destination"),
                DateOnly.FromDateTime(Details.PreferredPickupDate!.Value),
                Details.SpecialInstructions,
                Register.ServiceArea,
                Now),
            $"Request {Reference} updated.");
        return saved ? RedirectToPage(new { reference = Reference }) : Page();
    }

    /// <summary>Asks office staff to confirm the response the customer gave by phone.</summary>
    public IActionResult OnPostChooseResponse(bool accept)
    {
        ModelState.Clear();
        if (!Load())
        {
            return NotFoundMessage($"No request with reference '{Reference}' was found.");
        }

        PendingResponse = accept;
        FillQuoteFromCurrentOrSuggestion();
        return Page();
    }

    /// <summary>Records the confirmed response on the customer's behalf (A1 T3 variant 2a).</summary>
    public IActionResult OnPostConfirmResponse(bool accept)
    {
        ModelState.Clear();
        if (!Load())
        {
            return NotFoundMessage($"No request with reference '{Reference}' was found.");
        }

        Delivery? delivery = null;
        if (!TryChange(() => delivery = Register.RecordQuoteResponse(DeliveryRequest!, accept, Now)))
        {
            FillQuoteFromCurrentOrSuggestion();
            return Page();
        }

        StatusMessage = delivery is null
            ? $"The customer's decision to decline the quote for {Reference} has been recorded."
            : $"Acceptance recorded. Delivery {delivery.Id} is now waiting for staff allocation.";
        return RedirectToPage(new { reference = Reference });
    }

    private bool Load()
    {
        DeliveryRequest = Register.FindRequest(Reference);
        if (DeliveryRequest is null)
        {
            return false;
        }

        SuggestedCost = DeliveryRequest.SuggestCost(System.PricingPolicy);
        DateTime earliest = DeliveryRequest.PreferredPickupDate.ToDateTime(new TimeOnly(8, 0));
        DateTime soonest = RoundUpToHalfHour(Now.AddHours(1));
        SuggestedWindow = Roster.SuggestEarliestWindow(earliest > soonest ? earliest : soonest, SuggestedDuration);
        return true;
    }

    private void FillQuoteFromCurrentOrSuggestion()
    {
        if (DeliveryRequest!.Quote is { } current)
        {
            Quote = new QuoteInput { Cost = current.Cost, EstimatedPickup = current.Window.Start, EstimatedDelivery = current.Window.End };
        }
        else
        {
            Quote = new QuoteInput
            {
                Cost = SuggestedCost,
                EstimatedPickup = SuggestedWindow?.Start,
                EstimatedDelivery = SuggestedWindow?.End,
            };
        }

        if (Quote.EstimatedPickup is { } start && Quote.EstimatedDelivery is { } end && end > start)
        {
            StaffForEnteredWindow = Roster.FindSuitableStaff(new DeliveryWindow(start, end));
        }
    }

    private void KeepValidationFor(string prefix)
    {
        foreach (string key in ModelState.Keys.Where(key => key.Length > 0 && !key.StartsWith(prefix + ".", StringComparison.Ordinal)).ToList())
        {
            ModelState.Remove(key);
        }
    }

    private static DateTime RoundUpToHalfHour(DateTime value)
    {
        var halfHour = TimeSpan.FromMinutes(30);
        long remainder = value.Ticks % halfHour.Ticks;
        return remainder == 0 ? value : value.AddTicks(halfHour.Ticks - remainder);
    }

    /// <summary>Quote form fields (A1 T2 subtasks 2 to 4).</summary>
    public sealed class QuoteInput
    {
        /// <summary>Gets or sets the delivery cost in dollars.</summary>
        [Required(ErrorMessage = "Enter the delivery cost.")]
        [Range(typeof(decimal), "0.01", "5000", ErrorMessage = "The delivery cost must be between $0.01 and $5,000.")]
        [Display(Name = "Delivery cost ($)")]
        public decimal? Cost { get; set; }

        /// <summary>Gets or sets the estimated pickup time.</summary>
        [Required(ErrorMessage = "Enter the estimated pickup date and time.")]
        [Display(Name = "Estimated pickup")]
        public DateTime? EstimatedPickup { get; set; }

        /// <summary>Gets or sets the estimated delivery time.</summary>
        [Required(ErrorMessage = "Enter the estimated delivery date and time.")]
        [Display(Name = "Estimated delivery")]
        public DateTime? EstimatedDelivery { get; set; }

        /// <summary>Converts to the domain data-holder.</summary>
        public QuoteTerms ToTerms() => new(Cost ?? 0m, EstimatedPickup ?? DateTime.MinValue, EstimatedDelivery ?? DateTime.MinValue);

        /// <summary>Converts the entered times to a window.</summary>
        public DeliveryWindow ToWindow() => new(EstimatedPickup!.Value, EstimatedDelivery!.Value);
    }

    /// <summary>Editable request fields (customer and parcels are fixed once recorded).</summary>
    public sealed class RequestDetailsInput
    {
        /// <summary>Gets or sets the pickup address.</summary>
        public AddressInput Pickup { get; set; } = new();

        /// <summary>Gets or sets the destination address.</summary>
        public AddressInput Destination { get; set; } = new();

        /// <summary>Gets or sets the preferred pickup date.</summary>
        [Required(ErrorMessage = "Choose a preferred pickup date.")]
        [DataType(DataType.Date)]
        [Display(Name = "Preferred pickup date")]
        public DateTime? PreferredPickupDate { get; set; }

        /// <summary>Gets or sets optional instructions.</summary>
        [StringLength(300, ErrorMessage = "Special instructions must be 300 characters or fewer.")]
        [Display(Name = "Special instructions (optional)")]
        public string? SpecialInstructions { get; set; }

        /// <summary>Creates the input from an existing request.</summary>
        public static RequestDetailsInput From(DeliveryRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return new RequestDetailsInput
            {
                Pickup = AddressInput.From(request.PickupAddress),
                Destination = AddressInput.From(request.DestinationAddress),
                PreferredPickupDate = request.PreferredPickupDate.ToDateTime(TimeOnly.MinValue),
                SpecialInstructions = request.SpecialInstructions,
            };
        }
    }
}

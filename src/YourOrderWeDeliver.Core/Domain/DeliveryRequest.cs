using YourOrderWeDeliver.Core.Common;
using YourOrderWeDeliver.Core.Coordination;
using YourOrderWeDeliver.Core.Pricing;

namespace YourOrderWeDeliver.Core.Domain;

/// <summary>
/// A customer's request for one or more parcels to be delivered, before the quoted service is
/// accepted (A2 CRC 5.2). Created and registered by <see cref="DeliveryRegister"/>.
/// </summary>
public sealed class DeliveryRequest
{
    /// <summary>Most parcels accepted in a single request.</summary>
    public const int MaxParcels = 10;

    /// <summary>How far ahead a preferred pickup date may be chosen.</summary>
    public const int MaxDaysAhead = 60;

    private readonly List<Parcel> _parcels;

    internal DeliveryRequest(
        string reference,
        Customer customer,
        Address pickupAddress,
        Address destinationAddress,
        DateOnly preferredPickupDate,
        string? specialInstructions,
        IEnumerable<Parcel> parcels,
        RequestChannel channel,
        DateTime submittedAt)
    {
        Reference = reference;
        Customer = customer;
        PickupAddress = pickupAddress;
        DestinationAddress = destinationAddress;
        PreferredPickupDate = preferredPickupDate;
        SpecialInstructions = specialInstructions;
        Channel = channel;
        SubmittedAt = submittedAt;
        _parcels = parcels.ToList();
        foreach (Parcel parcel in _parcels)
        {
            parcel.Request = this;
        }
    }

    /// <summary>Gets the unique reference given to the customer, for example "REQ-100001" (R5).</summary>
    public string Reference { get; }

    /// <summary>Gets the customer who made the request (R1).</summary>
    public Customer Customer { get; }

    /// <summary>Gets the pickup address (R2).</summary>
    public Address PickupAddress { get; private set; }

    /// <summary>Gets the destination address (R3).</summary>
    public Address DestinationAddress { get; private set; }

    /// <summary>Gets the day the customer would like the parcels collected.</summary>
    public DateOnly PreferredPickupDate { get; private set; }

    /// <summary>Gets optional notes for staff.</summary>
    public string? SpecialInstructions { get; private set; }

    /// <summary>Gets how the request entered the system.</summary>
    public RequestChannel Channel { get; }

    /// <summary>Gets when the request was recorded.</summary>
    public DateTime SubmittedAt { get; }

    /// <summary>Gets the parcels included in the request (R4).</summary>
    public IReadOnlyList<Parcel> Parcels => _parcels;

    /// <summary>Gets the quote prepared for the request, if any (R8).</summary>
    public DeliveryQuote? Quote { get; internal set; }

    /// <summary>Gets the operational delivery created when the quote was accepted (R9).</summary>
    public Delivery? Delivery { get; internal set; }

    /// <summary>Gets the combined weight of all parcels.</summary>
    public decimal TotalWeightKg => _parcels.Sum(parcel => parcel.WeightKg);

    /// <summary>Gets the current stage of the request, derived from its quote and delivery.</summary>
    public RequestStatus Status => Quote?.Response switch
    {
        null => RequestStatus.Submitted,
        QuoteResponse.Pending => RequestStatus.Quoted,
        QuoteResponse.Accepted => RequestStatus.Accepted,
        _ => RequestStatus.Declined,
    };

    /// <summary>Changes the locations, date or instructions while no quote has been prepared.</summary>
    public void UpdateDetails(
        Address pickupAddress,
        Address destinationAddress,
        DateOnly preferredPickupDate,
        string? specialInstructions,
        ServiceArea serviceArea,
        DateTime now)
    {
        ArgumentNullException.ThrowIfNull(pickupAddress);
        ArgumentNullException.ThrowIfNull(destinationAddress);
        ArgumentNullException.ThrowIfNull(serviceArea);
        Require.That(
            Status == RequestStatus.Submitted,
            $"Request {Reference} can no longer be edited because a quote has been prepared. Revise the quote instead.");
        ValidateLocations(pickupAddress, destinationAddress, serviceArea);
        ValidatePreferredPickupDate(preferredPickupDate, now);
        PickupAddress = pickupAddress;
        DestinationAddress = destinationAddress;
        PreferredPickupDate = preferredPickupDate;
        SpecialInstructions = NormaliseInstructions(specialInstructions);
    }

    /// <summary>Calculates a suggested cost for office staff using the configured pricing policy.</summary>
    public decimal SuggestCost(IPricingPolicy pricingPolicy)
    {
        ArgumentNullException.ThrowIfNull(pricingPolicy);
        return pricingPolicy.CalculateCost(this);
    }

    /// <summary>
    /// Prepares the quote, or revises it if the customer has not responded yet (R7, R8).
    /// The roster is consulted so that only feasible timings are quoted (A2 DeliveryQuote R3).
    /// </summary>
    public DeliveryQuote PrepareQuote(QuoteTerms terms, StaffRoster roster, DateTime now)
    {
        ValidateQuote(terms, roster, now);
        if (Quote is null)
        {
            Quote = new DeliveryQuote($"{Reference}-Q", this, terms, now);
        }
        else
        {
            Quote.Revise(terms, now);
        }

        return Quote;
    }

    /// <summary>
    /// Applies every check of <see cref="PrepareQuote"/> without recording anything, so office staff
    /// can review the quote before issuing it (A1 T2 subtask 5).
    /// </summary>
    public void ValidateQuote(QuoteTerms terms, StaffRoster roster, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(roster);
        Require.That(
            Status is RequestStatus.Submitted or RequestStatus.Quoted,
            $"A quote cannot be prepared for request {Reference} because the customer has already responded.");
        DeliveryQuote.ValidateTerms(terms, roster, now);
    }

    internal static void ValidateLocations(Address pickupAddress, Address destinationAddress, ServiceArea serviceArea)
    {
        Require.That(
            serviceArea.Covers(pickupAddress),
            $"Pickup postcode {pickupAddress.Postcode} is outside our {serviceArea.Name} service area.");
        Require.That(
            serviceArea.Covers(destinationAddress),
            $"Destination postcode {destinationAddress.Postcode} is outside our {serviceArea.Name} service area.");
        Require.That(
            !pickupAddress.IsSamePlaceAs(destinationAddress),
            "The pickup and destination addresses are the same. Please check the destination address.");
    }

    internal static void ValidatePreferredPickupDate(DateOnly date, DateTime now)
    {
        DateOnly today = DateOnly.FromDateTime(now);
        Require.That(date >= today, "The preferred pickup date cannot be in the past. Choose today or a later date.");
        Require.That(
            date <= today.AddDays(MaxDaysAhead),
            $"The preferred pickup date must be within the next {MaxDaysAhead} days.");
    }

    internal static string? NormaliseInstructions(string? instructions)
    {
        return Require.OptionalText(instructions, "Special instructions", 300);
    }
}

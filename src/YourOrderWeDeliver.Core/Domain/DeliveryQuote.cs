using YourOrderWeDeliver.Core.Common;
using YourOrderWeDeliver.Core.Coordination;

namespace YourOrderWeDeliver.Core.Domain;

/// <summary>
/// The proposed cost and timing for a <see cref="DeliveryRequest"/>, together with the customer's
/// response (A2 CRC 5.3). A quote may be revised until the customer responds.
/// </summary>
public sealed class DeliveryQuote
{
    /// <summary>Most a single delivery may be quoted at.</summary>
    public const decimal MaxCost = 5000m;

    /// <summary>How long a quote can be accepted after it is prepared (A1 assumption A14).</summary>
    public static readonly TimeSpan ValidityPeriod = TimeSpan.FromDays(7);

    /// <summary>Longest time allowed between estimated pickup and delivery.</summary>
    public static readonly TimeSpan MaxDeliveryDuration = TimeSpan.FromHours(12);

    internal DeliveryQuote(string id, DeliveryRequest request, QuoteTerms terms, DateTime preparedAt)
    {
        Id = id;
        Request = request;
        Revision = 1;
        Apply(terms, preparedAt);
    }

    /// <summary>Gets the identifier, for example "REQ-100001-Q".</summary>
    public string Id { get; }

    /// <summary>Gets the request this quote was prepared for (R1).</summary>
    public DeliveryRequest Request { get; }

    /// <summary>Gets the proposed delivery cost (R2).</summary>
    public decimal Cost { get; private set; }

    /// <summary>Gets the estimated pickup-to-delivery window (R3, R4).</summary>
    public DeliveryWindow Window { get; private set; }

    /// <summary>Gets when the current revision was prepared.</summary>
    public DateTime PreparedAt { get; internal set; }

    /// <summary>Gets the revision number, starting at 1 (R5).</summary>
    public int Revision { get; internal set; }

    /// <summary>Gets the customer's response (R6).</summary>
    public QuoteResponse Response { get; internal set; }

    /// <summary>Gets when the customer responded.</summary>
    public DateTime? RespondedAt { get; internal set; }

    /// <summary>Gets the last moment at which the customer may accept.</summary>
    public DateTime ValidUntil => PreparedAt + ValidityPeriod;

    /// <summary>Gets whether the request may proceed to an operational delivery (R7).</summary>
    public bool MayProceedToDelivery => Response == QuoteResponse.Accepted;

    /// <summary>Determines whether the quote can no longer be accepted.</summary>
    public bool IsExpired(DateTime now) => Response == QuoteResponse.Pending && now > ValidUntil;

    internal static void ValidateTerms(QuoteTerms terms, StaffRoster roster, DateTime now)
    {
        Require.That(
            terms.Cost > 0 && terms.Cost <= MaxCost,
            $"The delivery cost must be more than $0 and no more than {DisplayFormat.Money(MaxCost)}.");
        Require.That(decimal.Round(terms.Cost, 2) == terms.Cost, "The delivery cost can have at most two decimal places.");
        Require.That(terms.EstimatedPickup > now, "The estimated pickup time must be in the future.");
        Require.That(
            terms.EstimatedDelivery > terms.EstimatedPickup,
            "The estimated delivery time must be later than the estimated pickup time.");

        var window = new DeliveryWindow(terms.EstimatedPickup, terms.EstimatedDelivery);
        Require.That(
            window.Duration <= MaxDeliveryDuration,
            $"The estimated delivery time must be within {MaxDeliveryDuration.TotalHours} hours of pickup.");

        if (!roster.HasAvailableStaff(window))
        {
            DeliveryWindow? suggestion = roster.SuggestEarliestWindow(terms.EstimatedPickup, window.Duration);
            string nextStep = suggestion is { } alternative
                ? $" The earliest time with available staff is {DisplayFormat.Window(alternative)}."
                : " No staff have availability recorded after this time. Update staff availability first.";
            throw new BusinessRuleException(
                "No active delivery staff are available for the proposed pickup and delivery times." + nextStep);
        }
    }

    internal void Revise(QuoteTerms terms, DateTime now)
    {
        Require.That(
            Response == QuoteResponse.Pending,
            "This quote can no longer be revised because the customer has already responded.");
        Apply(terms, now);
        Revision++;
    }

    internal void RecordResponse(bool accepted, DateTime now)
    {
        Require.That(
            Response == QuoteResponse.Pending,
            $"The customer has already {(Response == QuoteResponse.Accepted ? "accepted" : "declined")} this quote. "
            + "A response can only be recorded once.");
        Require.That(
            !IsExpired(now),
            $"This quote expired on {DisplayFormat.DateAndTime(ValidUntil)}. "
            + "Please contact our office so the quote can be revised.");
        Response = accepted ? QuoteResponse.Accepted : QuoteResponse.Declined;
        RespondedAt = now;
    }

    private void Apply(QuoteTerms terms, DateTime preparedAt)
    {
        Cost = terms.Cost;
        Window = new DeliveryWindow(terms.EstimatedPickup, terms.EstimatedDelivery);
        PreparedAt = preparedAt;
    }
}

namespace YourOrderWeDeliver.Core.Domain;

/// <summary>Data-holder: unvalidated address fields entered by a user.</summary>
/// <param name="Street">Street number and name.</param>
/// <param name="Suburb">Suburb.</param>
/// <param name="Postcode">Postcode.</param>
public sealed record AddressDetails(string? Street, string? Suburb, string? Postcode);

/// <summary>Data-holder: unvalidated parcel fields entered by a user.</summary>
/// <param name="Description">What the parcel contains.</param>
/// <param name="WeightKg">Weight in kilograms.</param>
/// <param name="IsFragile">Whether the parcel needs careful handling.</param>
public sealed record ParcelDetails(string? Description, decimal WeightKg, bool IsFragile);

/// <summary>Data-holder: everything a user enters when submitting a delivery request (A1 task T1).</summary>
/// <param name="CustomerName">Customer's full name.</param>
/// <param name="CustomerEmail">Customer's email address; identifies returning customers.</param>
/// <param name="CustomerPhone">Customer's Australian phone number.</param>
/// <param name="Pickup">Pickup address.</param>
/// <param name="Destination">Destination address.</param>
/// <param name="PreferredPickupDate">Day the customer would like the parcels collected.</param>
/// <param name="SpecialInstructions">Optional notes for office and delivery staff.</param>
/// <param name="Parcels">One or more parcels.</param>
public sealed record RequestDetails(
    string? CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    AddressDetails Pickup,
    AddressDetails Destination,
    DateOnly PreferredPickupDate,
    string? SpecialInstructions,
    IReadOnlyList<ParcelDetails> Parcels);

/// <summary>Data-holder: cost and timing proposed by office staff (A1 task T2).</summary>
/// <param name="Cost">Proposed delivery cost in Australian dollars.</param>
/// <param name="EstimatedPickup">Estimated pickup time.</param>
/// <param name="EstimatedDelivery">Estimated delivery time.</param>
public sealed record QuoteTerms(decimal Cost, DateTime EstimatedPickup, DateTime EstimatedDelivery);

/// <summary>Data-holder: an entry in a delivery's history, shown when tracking (A1 task T8).</summary>
/// <param name="At">When the event happened.</param>
/// <param name="Description">What happened.</param>
/// <param name="IsIssue">Whether the entry reports a problem that needs attention.</param>
public sealed record DeliveryEvent(DateTime At, string Description, bool IsIssue);

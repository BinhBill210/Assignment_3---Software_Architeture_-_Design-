using YourOrderWeDeliver.Core.Domain;

namespace YourOrderWeDeliver.Core.Persistence;

/// <summary>
/// Data-holder: the complete persisted state of the system. Object references are stored as
/// identifiers and are relinked during start-up (A2 Section 7).
/// </summary>
public sealed class SystemSnapshot
{
    /// <summary>Gets or sets the file format version.</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Gets or sets the last identifier numbers issued.</summary>
    public CounterRecord Counters { get; set; } = new(0, 100000, 0, 0);

    /// <summary>Gets or sets delivery staff and their availability.</summary>
    public IReadOnlyList<StaffRecord> Staff { get; set; } = [];

    /// <summary>Gets or sets customers.</summary>
    public IReadOnlyList<CustomerRecord> Customers { get; set; } = [];

    /// <summary>Gets or sets delivery requests with their parcels and quotes.</summary>
    public IReadOnlyList<RequestRecord> Requests { get; set; } = [];

    /// <summary>Gets or sets deliveries with their payments and history.</summary>
    public IReadOnlyList<DeliveryRecord> Deliveries { get; set; } = [];
}

/// <summary>Data-holder: last identifier numbers issued.</summary>
/// <param name="Customer">Last customer number.</param>
/// <param name="Request">Last request number.</param>
/// <param name="Delivery">Last delivery number.</param>
/// <param name="Staff">Last staff number.</param>
public sealed record CounterRecord(int Customer, int Request, int Delivery, int Staff);

/// <summary>Data-holder: persisted customer.</summary>
/// <param name="Id">Identifier.</param>
/// <param name="Name">Name.</param>
/// <param name="Email">Email.</param>
/// <param name="Phone">Phone.</param>
public sealed record CustomerRecord(string Id, string Name, string Email, string Phone);

/// <summary>Data-holder: persisted availability period.</summary>
/// <param name="Id">Identifier.</param>
/// <param name="Start">Start.</param>
/// <param name="End">End.</param>
public sealed record AvailabilityPeriodRecord(string Id, DateTime Start, DateTime End);

/// <summary>Data-holder: persisted delivery staff member.</summary>
/// <param name="Id">Identifier.</param>
/// <param name="Name">Name.</param>
/// <param name="Phone">Phone.</param>
/// <param name="Email">Email.</param>
/// <param name="IsActive">Whether active.</param>
/// <param name="JoinedOn">Join date.</param>
/// <param name="LeftOn">Leave date.</param>
/// <param name="Availability">Availability periods.</param>
public sealed record StaffRecord(
    string Id,
    string Name,
    string Phone,
    string Email,
    bool IsActive,
    DateOnly JoinedOn,
    DateOnly? LeftOn,
    IReadOnlyList<AvailabilityPeriodRecord> Availability);

/// <summary>Data-holder: persisted address.</summary>
/// <param name="Street">Street.</param>
/// <param name="Suburb">Suburb.</param>
/// <param name="Postcode">Postcode.</param>
public sealed record AddressRecord(string Street, string Suburb, string Postcode);

/// <summary>Data-holder: persisted parcel.</summary>
/// <param name="Id">Identifier.</param>
/// <param name="Description">Description.</param>
/// <param name="WeightKg">Weight.</param>
/// <param name="IsFragile">Whether fragile.</param>
/// <param name="CollectedAt">Collection time.</param>
/// <param name="DeliveredAt">Delivery time.</param>
public sealed record ParcelRecord(
    string Id, string Description, decimal WeightKg, bool IsFragile, DateTime? CollectedAt, DateTime? DeliveredAt);

/// <summary>Data-holder: persisted quote.</summary>
/// <param name="Id">Identifier.</param>
/// <param name="Cost">Cost.</param>
/// <param name="EstimatedPickup">Estimated pickup.</param>
/// <param name="EstimatedDelivery">Estimated delivery.</param>
/// <param name="PreparedAt">Preparation time.</param>
/// <param name="Revision">Revision number.</param>
/// <param name="Response">Customer response.</param>
/// <param name="RespondedAt">Response time.</param>
public sealed record QuoteRecord(
    string Id,
    decimal Cost,
    DateTime EstimatedPickup,
    DateTime EstimatedDelivery,
    DateTime PreparedAt,
    int Revision,
    QuoteResponse Response,
    DateTime? RespondedAt);

/// <summary>Data-holder: persisted delivery request.</summary>
/// <param name="Reference">Reference.</param>
/// <param name="CustomerId">Customer identifier.</param>
/// <param name="Pickup">Pickup address.</param>
/// <param name="Destination">Destination address.</param>
/// <param name="PreferredPickupDate">Preferred pickup date.</param>
/// <param name="SpecialInstructions">Instructions.</param>
/// <param name="Channel">Channel.</param>
/// <param name="SubmittedAt">Submission time.</param>
/// <param name="Parcels">Parcels.</param>
/// <param name="Quote">Quote, if prepared.</param>
public sealed record RequestRecord(
    string Reference,
    string CustomerId,
    AddressRecord Pickup,
    AddressRecord Destination,
    DateOnly PreferredPickupDate,
    string? SpecialInstructions,
    RequestChannel Channel,
    DateTime SubmittedAt,
    IReadOnlyList<ParcelRecord> Parcels,
    QuoteRecord? Quote);

/// <summary>Data-holder: persisted payment.</summary>
/// <param name="Id">Identifier.</param>
/// <param name="Amount">Amount.</param>
/// <param name="Method">Method.</param>
/// <param name="ReceivedByStaffId">Receiving staff member.</param>
/// <param name="ReceivedAt">Receipt time.</param>
public sealed record PaymentRecord(string Id, decimal Amount, PaymentMethod Method, string ReceivedByStaffId, DateTime ReceivedAt);

/// <summary>Data-holder: persisted delivery.</summary>
/// <param name="Id">Identifier.</param>
/// <param name="RequestReference">Originating request.</param>
/// <param name="CreatedAt">Creation time.</param>
/// <param name="Status">Status.</param>
/// <param name="AllocatedStaffId">Allocated staff member.</param>
/// <param name="Payment">Payment, if recorded.</param>
/// <param name="PickedUpAt">Pickup time.</param>
/// <param name="DeliveredAt">Completion time.</param>
/// <param name="ReceivedBy">Recipient name.</param>
/// <param name="History">History entries.</param>
public sealed record DeliveryRecord(
    string Id,
    string RequestReference,
    DateTime CreatedAt,
    DeliveryStatus Status,
    string? AllocatedStaffId,
    PaymentRecord? Payment,
    DateTime? PickedUpAt,
    DateTime? DeliveredAt,
    string? ReceivedBy,
    IReadOnlyList<DeliveryEvent> History);

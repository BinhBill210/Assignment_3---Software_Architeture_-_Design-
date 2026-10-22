using YourOrderWeDeliver.Core.Common;
using YourOrderWeDeliver.Core.Coordination;

namespace YourOrderWeDeliver.Core.Domain;

/// <summary>
/// An accepted, operational delivery from staff allocation through pickup to completion
/// (A2 CRC 5.4). Each state change is checked against the contracts in A2 Table 6.
/// </summary>
public sealed class Delivery
{
    private readonly List<DeliveryEvent> _history = [];

    internal Delivery(string id, DeliveryRequest request, DateTime createdAt)
    {
        DeliveryQuote quote = request.Quote
            ?? throw new InvalidOperationException("A delivery can only be created from a quoted request.");
        Id = id;
        Request = request;
        CreatedAt = createdAt;
        Window = quote.Window;
        foreach (Parcel parcel in request.Parcels)
        {
            parcel.Delivery = this;
        }
    }

    /// <summary>Gets the identifier, for example "DEL-000001".</summary>
    public string Id { get; }

    /// <summary>Gets the request the delivery originated from (R1).</summary>
    public DeliveryRequest Request { get; }

    /// <summary>Gets the parcels being transported (R2).</summary>
    public IReadOnlyList<Parcel> Parcels => Request.Parcels;

    /// <summary>Gets the scheduled pickup-to-delivery window taken from the accepted quote.</summary>
    public DeliveryWindow Window { get; }

    /// <summary>Gets when the quote was accepted and the delivery created.</summary>
    public DateTime CreatedAt { get; }

    /// <summary>Gets the current status (R5).</summary>
    public DeliveryStatus Status { get; internal set; } = DeliveryStatus.AwaitingAllocation;

    /// <summary>Gets the staff member currently allocated, if any (R3).</summary>
    public DeliveryStaff? AllocatedStaff { get; internal set; }

    /// <summary>Gets the payment recorded at pickup, if any (R8).</summary>
    public Payment? Payment { get; internal set; }

    /// <summary>Gets when pickup was recorded.</summary>
    public DateTime? PickedUpAt { get; internal set; }

    /// <summary>Gets when completion was recorded.</summary>
    public DateTime? DeliveredAt { get; internal set; }

    /// <summary>Gets the name of the person who received the parcels.</summary>
    public string? ReceivedBy { get; internal set; }

    /// <summary>Gets the history used for tracking and traceability (R10).</summary>
    public IReadOnlyList<DeliveryEvent> History => _history;

    /// <summary>Gets how many parcels have been collected (R6).</summary>
    public int CollectedParcelCount => Parcels.Count(parcel => parcel.IsCollected);

    /// <summary>Gets how many parcels have been delivered (R6).</summary>
    public int DeliveredParcelCount => Parcels.Count(parcel => parcel.IsDelivered);

    /// <summary>Gets whether the delivery was completed by the end of its scheduled window.</summary>
    public bool IsOnTime => DeliveredAt is { } deliveredAt && deliveredAt <= Window.End;

    /// <summary>Gets a short plain-language description of progress (R6).</summary>
    public string ProgressSummary => Status switch
    {
        DeliveryStatus.AwaitingAllocation => "Waiting for a delivery staff member to be allocated.",
        DeliveryStatus.Allocated =>
            $"Allocated to {AllocatedStaff?.Name}. {CollectedParcelCount} of {Parcels.Count} parcel(s) collected.",
        DeliveryStatus.PickedUp =>
            $"Picked up and on the way. {DeliveredParcelCount} of {Parcels.Count} parcel(s) delivered.",
        _ => $"Delivered on {DisplayFormat.DateAndTime(DeliveredAt!.Value)}"
            + (ReceivedBy is null ? "." : $", received by {ReceivedBy}."),
    };

    /// <summary>
    /// Records or changes the allocated staff member (R4). The roster confirms the staff member is
    /// active, available and not already busy at that time.
    /// </summary>
    public void AllocateStaff(DeliveryStaff staff, StaffRoster roster, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(staff);
        ArgumentNullException.ThrowIfNull(roster);
        Require.That(
            Status is DeliveryStatus.AwaitingAllocation or DeliveryStatus.Allocated,
            $"Delivery {Id} has already been picked up, so its delivery staff member can no longer be changed.");
        Require.That(!ReferenceEquals(staff, AllocatedStaff), $"{staff.Name} is already allocated to delivery {Id}.");

        string? reason = roster.DescribeUnsuitability(staff, Window, this);
        if (reason is not null)
        {
            throw new BusinessRuleException(
                $"{staff.Name} cannot be allocated to delivery {Id} because {reason} "
                + "Choose another staff member or revise the schedule.");
        }

        DeliveryStaff? previous = AllocatedStaff;
        previous?.ReleaseDelivery(this);
        staff.AssignDelivery(this);
        AllocatedStaff = staff;
        Status = DeliveryStatus.Allocated;
        Record(
            now,
            previous is null
                ? $"Allocated to {staff.Name} ({staff.Id})."
                : $"Reassigned from {previous.Name} ({previous.Id}) to {staff.Name} ({staff.Id}).");
    }

    /// <summary>Records that one parcel has been collected by the allocated staff member (R7).</summary>
    public void RecordParcelCollected(string parcelId, DeliveryStaff performedBy, DateTime now)
    {
        EnsurePerformedByAllocatedStaff(performedBy, "record parcel collection");
        Require.That(Status == DeliveryStatus.Allocated, $"Parcels for delivery {Id} can only be collected before pickup is recorded.");
        Parcel parcel = FindParcel(parcelId);
        parcel.MarkCollected(now);
        Record(now, $"Parcel {parcel.Id} collected.");
    }

    /// <summary>Reverses a collection mark entered by mistake, before pickup is recorded.</summary>
    public void UndoParcelCollected(string parcelId, DeliveryStaff performedBy, DateTime now)
    {
        EnsurePerformedByAllocatedStaff(performedBy, "change parcel collection");
        Require.That(Status == DeliveryStatus.Allocated, "Collection can no longer be changed because pickup has been recorded.");
        Parcel parcel = FindParcel(parcelId);
        parcel.UndoCollected();
        Record(now, $"Collection of parcel {parcel.Id} was undone.");
    }

    /// <summary>
    /// Records that pickup is complete and creates the payment collected at pickup (R7, R8).
    /// No real payment is processed; the method and amount are recorded only.
    /// </summary>
    public Payment RecordPickup(PaymentMethod method, DeliveryStaff performedBy, DateTime now)
    {
        EnsurePerformedByAllocatedStaff(performedBy, "record pickup");
        Require.That(Status == DeliveryStatus.Allocated, $"Pickup has already been recorded for delivery {Id}.");
        Require.That(
            CollectedParcelCount == Parcels.Count,
            $"Pickup cannot be recorded yet: only {CollectedParcelCount} of {Parcels.Count} parcel(s) are marked as collected. "
            + "Mark every parcel as collected first.");
        Require.That(Enum.IsDefined(method), "Choose how the customer paid.");

        Payment = new Payment($"{Id}-PAY", this, Request.Quote!.Cost, method, performedBy.Id, now);
        PickedUpAt = now;
        Status = DeliveryStatus.PickedUp;
        Record(now, $"Pickup recorded. Payment of {DisplayFormat.Money(Payment.Amount)} received by {method}.");
        return Payment;
    }

    /// <summary>Records that one parcel has been handed over at the destination.</summary>
    public void RecordParcelDelivered(string parcelId, DeliveryStaff performedBy, DateTime now)
    {
        EnsurePerformedByAllocatedStaff(performedBy, "record parcel delivery");
        Require.That(Status == DeliveryStatus.PickedUp, $"Parcels for delivery {Id} can only be delivered after pickup is recorded.");
        Parcel parcel = FindParcel(parcelId);
        parcel.MarkDelivered(now);
        Record(now, $"Parcel {parcel.Id} delivered.");
    }

    /// <summary>Reverses a delivery mark entered by mistake, before completion is recorded.</summary>
    public void UndoParcelDelivered(string parcelId, DeliveryStaff performedBy, DateTime now)
    {
        EnsurePerformedByAllocatedStaff(performedBy, "change parcel delivery");
        Require.That(Status == DeliveryStatus.PickedUp, "Parcel delivery can no longer be changed because the delivery is complete.");
        Parcel parcel = FindParcel(parcelId);
        parcel.UndoDelivered();
        Record(now, $"Delivery of parcel {parcel.Id} was undone.");
    }

    /// <summary>Records successful completion once every parcel is delivered (R9).</summary>
    public void RecordCompletion(string? recipientName, DeliveryStaff performedBy, DateTime now)
    {
        EnsurePerformedByAllocatedStaff(performedBy, "record completion");
        Require.That(Status == DeliveryStatus.PickedUp, $"Delivery {Id} must be picked up before it can be completed.");
        Require.That(
            DeliveredParcelCount == Parcels.Count,
            $"Completion cannot be recorded yet: only {DeliveredParcelCount} of {Parcels.Count} parcel(s) are marked as delivered. "
            + "Mark every parcel as delivered first.");
        ReceivedBy = Require.Text(recipientName, "Received by (recipient name)", 100);
        DeliveredAt = now;
        Status = DeliveryStatus.Delivered;
        Record(now, $"Delivery completed. Received by {ReceivedBy}.");
    }

    /// <summary>Records a problem reported by staff, shown as an issue when tracking (A1 T8 subtask 5).</summary>
    public void RecordIssue(string? description, string reportedBy, DateTime now)
    {
        string text = Require.Text(description, "Issue description", 300);
        Require.That(Status != DeliveryStatus.Delivered, $"Delivery {Id} is complete; issues can no longer be added.");
        Record(now, $"Issue reported by {reportedBy}: {text}", isIssue: true);
    }

    internal void ReturnToAllocationQueue(string reason, DateTime now)
    {
        DeliveryStaff? previous = AllocatedStaff;
        previous?.ReleaseDelivery(this);
        AllocatedStaff = null;
        Status = DeliveryStatus.AwaitingAllocation;
        Record(now, $"Allocation to {previous?.Name ?? "staff"} removed: {reason} Waiting for reallocation.", isIssue: true);
    }

    internal void Record(DateTime at, string description, bool isIssue = false)
    {
        _history.Add(new DeliveryEvent(at, description, isIssue));
    }

    private Parcel FindParcel(string parcelId)
    {
        return Parcels.FirstOrDefault(parcel => string.Equals(parcel.Id, parcelId, StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException($"Parcel {parcelId} is not part of delivery {Id}.");
    }

    private void EnsurePerformedByAllocatedStaff(DeliveryStaff performedBy, string activity)
    {
        ArgumentNullException.ThrowIfNull(performedBy);
        Require.That(
            ReferenceEquals(performedBy, AllocatedStaff),
            $"Only the delivery staff member allocated to delivery {Id} can {activity}.");
    }
}

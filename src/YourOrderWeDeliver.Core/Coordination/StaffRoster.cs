using System.Globalization;
using YourOrderWeDeliver.Core.Common;
using YourOrderWeDeliver.Core.Domain;

namespace YourOrderWeDeliver.Core.Coordination;

/// <summary>Data-holder: the outcome of reviewing one allocation after a staff change.</summary>
/// <param name="At">When the review happened.</param>
/// <param name="DeliveryId">Delivery that was reviewed.</param>
/// <param name="FromStaffName">Staff member who was allocated.</param>
/// <param name="ToStaffName">Replacement staff member, or <c>null</c> when none could be found.</param>
/// <param name="Reason">Why the allocation was reviewed.</param>
public sealed record ReassignmentRecord(DateTime At, string DeliveryId, string FromStaffName, string? ToStaffName, string Reason)
{
    /// <summary>Gets whether a replacement staff member was allocated.</summary>
    public bool WasReassigned => ToStaffName is not null;

    /// <summary>Gets a one-line description for display.</summary>
    public string Summary => WasReassigned
        ? $"Delivery {DeliveryId} was reassigned from {FromStaffName} to {ToStaffName}."
        : $"Delivery {DeliveryId} was removed from {FromStaffName} and no replacement is available. "
          + "It is back in the allocation list for office staff.";
}

/// <summary>
/// The system-wide collection of delivery staff (A2 CRC 5.12). It selects suitable staff for
/// quoting and allocation and, as the Observer of every <see cref="DeliveryStaffAvailability"/>,
/// reassigns deliveries affected by an availability change (A2 Scenario 4).
/// </summary>
public sealed class StaffRoster : IAvailabilityObserver
{
    private readonly Dictionary<string, DeliveryStaff> _staff = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ReassignmentRecord> _reassignments = [];

    /// <summary>Gets every staff record, active and inactive, ordered by identifier (R1).</summary>
    public IEnumerable<DeliveryStaff> AllStaff => _staff.Values.OrderBy(staff => staff.Id, StringComparer.Ordinal);

    /// <summary>Gets staff eligible for new allocations (R2).</summary>
    public IEnumerable<DeliveryStaff> ActiveStaff => AllStaff.Where(staff => staff.IsActive);

    /// <summary>Gets reassignments made since start-up, newest first.</summary>
    public IReadOnlyList<ReassignmentRecord> ReassignmentLog => _reassignments;

    internal int LastStaffNumber { get; set; }

    /// <summary>Creates a record for a new staff member (A1 T10 subtask 2).</summary>
    public DeliveryStaff RegisterNewStaff(string name, string phone, string email, DateOnly joinedOn)
    {
        string validEmail = Require.Email(email);
        DeliveryStaff? existing = FindByEmail(validEmail, null);
        if (existing is not null)
        {
            throw new BusinessRuleException(
                $"A staff record for {validEmail} already exists ({existing.Id}, {existing.Name}). "
                + (existing.IsActive ? "Update that record instead." : "Reactivate that record instead of creating a new one."));
        }

        string id = string.Create(CultureInfo.InvariantCulture, $"DS-{LastStaffNumber + 1:000}");
        var staff = new DeliveryStaff(id, name, phone, validEmail, joinedOn);
        LastStaffNumber++;
        Enrol(staff);
        return staff;
    }

    /// <summary>Updates contact details, keeping email addresses unique.</summary>
    public void UpdateStaffDetails(DeliveryStaff staff, string name, string phone, string email)
    {
        ArgumentNullException.ThrowIfNull(staff);
        string validEmail = Require.Email(email);
        DeliveryStaff? clash = FindByEmail(validEmail, staff);
        Require.That(clash is null, $"{validEmail} is already used by {clash?.Name} ({clash?.Id}).");
        staff.UpdateDetails(name, phone, validEmail);
    }

    /// <summary>Finds a staff record by identifier.</summary>
    public DeliveryStaff? Find(string? staffId)
    {
        return staffId is not null && _staff.TryGetValue(staffId.Trim(), out DeliveryStaff? staff) ? staff : null;
    }

    /// <summary>Finds a staff record by identifier or reports that it does not exist.</summary>
    public DeliveryStaff Get(string? staffId)
    {
        return Find(staffId) ?? throw new BusinessRuleException($"No delivery staff member with ID '{staffId}' was found.");
    }

    /// <summary>
    /// Explains why a staff member cannot take a delivery in <paramref name="window"/>,
    /// or returns <c>null</c> if they can (R2, R3).
    /// </summary>
    public string? DescribeUnsuitability(DeliveryStaff staff, DeliveryWindow window, Delivery? ignoring)
    {
        EnsureEnrolled(staff);
        if (!staff.IsActive)
        {
            return "they are no longer an active staff member.";
        }

        if (!staff.Availability.IsAvailableFor(window))
        {
            return $"they have no availability recorded covering {DisplayFormat.Window(window)}.";
        }

        Delivery? clash = staff.FindConflictingAllocation(window, ignoring);
        return clash is null
            ? null
            : $"they are already allocated to delivery {clash.Id} at an overlapping time ({DisplayFormat.Window(clash.Window)}).";
    }

    /// <summary>Lists active, available staff with no clashing allocation, least busy first (R3, R5).</summary>
    public IReadOnlyList<DeliveryStaff> FindSuitableStaff(DeliveryWindow window, Delivery? ignoring = null)
    {
        return ActiveStaff
            .Where(staff => DescribeUnsuitability(staff, window, ignoring) is null)
            .OrderBy(staff => staff.CurrentDeliveries.Count())
            .ThenBy(staff => staff.Name, StringComparer.CurrentCulture)
            .ToList();
    }

    /// <summary>Determines whether anyone could perform a delivery in the window (R4).</summary>
    public bool HasAvailableStaff(DeliveryWindow window) => FindSuitableStaff(window).Count > 0;

    /// <summary>
    /// Finds the earliest window of the given length, starting no earlier than
    /// <paramref name="earliestStart"/>, that some active staff member could take (A1 T2 variant 3a).
    /// </summary>
    public DeliveryWindow? SuggestEarliestWindow(DateTime earliestStart, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return null;
        }

        DeliveryWindow? best = null;
        foreach (DeliveryStaff staff in ActiveStaff)
        {
            foreach (AvailabilityPeriod period in staff.Availability.Periods)
            {
                IEnumerable<DateTime> candidateStarts = staff.CurrentDeliveries
                    .Select(delivery => delivery.Window.End)
                    .Append(period.Window.Start)
                    .Append(earliestStart)
                    .Where(start => start >= earliestStart && start >= period.Window.Start && start + duration <= period.Window.End)
                    .Order();

                foreach (DateTime start in candidateStarts)
                {
                    var candidate = new DeliveryWindow(start, start + duration);
                    if (staff.FindConflictingAllocation(candidate, null) is null)
                    {
                        if (best is null || candidate.Start < best.Value.Start)
                        {
                            best = candidate;
                        }

                        break;
                    }
                }
            }
        }

        return best;
    }

    /// <summary>
    /// Records that a staff member has left (A1 T10 subtask 4). Deliveries not yet picked up are
    /// reassigned or returned to the allocation list; history is kept (A2 assumption A5).
    /// </summary>
    public IReadOnlyList<ReassignmentRecord> DeactivateStaff(DeliveryStaff staff, DateOnly leftOn, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(staff);
        Require.That(staff.IsActive, $"{staff.Name} is already inactive.");
        Delivery? inTransit = staff.CurrentDeliveries.FirstOrDefault(
            delivery => delivery.CollectedParcelCount > 0
                || delivery.Status == DeliveryStatus.PickedUp);
        Require.That(
            inTransit is null,
            $"{staff.Name} is carrying parcels for delivery {inTransit?.Id}. Complete that delivery before recording that they have left.");

        staff.IsActive = false;
        staff.LeftOn = leftOn;
        return ReviewAllocations(staff, now, $"{staff.Name} has left the business.");
    }

    /// <summary>Reactivates the existing record of a staff member who rejoins (A1 T10 variant 4a).</summary>
    public void ReactivateStaff(DeliveryStaff staff, DateOnly rejoinedOn)
    {
        EnsureEnrolled(staff);
        Require.That(!staff.IsActive, $"{staff.Name} is already active.");
        staff.IsActive = true;
        staff.LeftOn = null;
        staff.JoinedOn = rejoinedOn;
    }

    /// <inheritdoc />
    void IAvailabilityObserver.ValidatePeriodRemoval(
        DeliveryStaffAvailability availability, AvailabilityPeriod period)
    {
        ArgumentNullException.ThrowIfNull(availability);
        ArgumentNullException.ThrowIfNull(period);

        Delivery? affected = availability.Staff.CurrentDeliveries.FirstOrDefault(
            delivery => delivery.CollectedParcelCount > 0
                && !availability.Periods.Any(
                    remaining => remaining.Id != period.Id
                        && remaining.Window.Contains(delivery.Window)));

        Require.That(
            affected is null,
            $"This availability period cannot be removed because delivery {affected?.Id} "
            + "has parcels marked as collected. Complete the delivery before removing its availability.");
    }
    /// <inheritdoc />
    void IAvailabilityObserver.OnAvailabilityChanged(DeliveryStaffAvailability availability, DateTime changedAt)
    {
        ArgumentNullException.ThrowIfNull(availability);
        ReviewAllocations(availability.Staff, changedAt, $"{availability.Staff.Name}'s availability changed.");
    }

    internal void Enrol(DeliveryStaff staff)
    {
        _staff.Add(staff.Id, staff);
        staff.Availability.Subscribe(this);
    }

    private List<ReassignmentRecord> ReviewAllocations(DeliveryStaff staff, DateTime now, string reason)
    {
        var outcomes = new List<ReassignmentRecord>();
        foreach (Delivery delivery in staff.CurrentDeliveries.Where(d => d.Status == DeliveryStatus.Allocated).ToList())
        {
            if (DescribeUnsuitability(staff, delivery.Window, delivery) is null)
            {
                continue;
            }

            DeliveryStaff? replacement = FindSuitableStaff(delivery.Window, delivery)
                .FirstOrDefault(candidate => !ReferenceEquals(candidate, staff));
            if (replacement is not null)
            {
                delivery.AllocateStaff(replacement, this, now);
            }
            else
            {
                delivery.ReturnToAllocationQueue(reason, now);
            }

            outcomes.Add(new ReassignmentRecord(now, delivery.Id, staff.Name, replacement?.Name, reason));
        }

        _reassignments.InsertRange(0, outcomes);
        return outcomes;
    }

    private void EnsureEnrolled(DeliveryStaff staff)
    {
        ArgumentNullException.ThrowIfNull(staff);
        if (!_staff.TryGetValue(staff.Id, out DeliveryStaff? enrolled) || !ReferenceEquals(enrolled, staff))
        {
            throw new InvalidOperationException($"Staff member {staff.Id} does not belong to this roster.");
        }
    }

    private DeliveryStaff? FindByEmail(string email, DeliveryStaff? ignoring)
    {
        return _staff.Values.FirstOrDefault(
            staff => !ReferenceEquals(staff, ignoring) && string.Equals(staff.Email, email, StringComparison.OrdinalIgnoreCase));
    }
}

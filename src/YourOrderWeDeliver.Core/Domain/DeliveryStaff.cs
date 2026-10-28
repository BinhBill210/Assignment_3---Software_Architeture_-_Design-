using YourOrderWeDeliver.Core.Common;
using YourOrderWeDeliver.Core.Coordination;

namespace YourOrderWeDeliver.Core.Domain;

/// <summary>
/// A delivery staff member who can be allocated to collect and deliver parcels (A2 CRC 5.7).
/// Records are never deleted: a staff member who leaves is deactivated and keeps their history.
/// Joining, leaving and rejoining are coordinated by <see cref="StaffRoster"/>.
/// </summary>
public sealed class DeliveryStaff
{
    private readonly List<Delivery> _deliveries = [];

    internal DeliveryStaff(string id, string name, string phone, string email, DateOnly joinedOn)
    {
        Id = id;
        Name = Require.PersonName(name, "Staff name", 100);
        Phone = Require.Phone(phone);
        Email = Require.Email(email);
        JoinedOn = joinedOn;
        IsActive = true;
        Availability = new DeliveryStaffAvailability(this);
    }

    /// <summary>Gets the identifier, also used to sign in, for example "DS-001".</summary>
    public string Id { get; }

    /// <summary>Gets the staff member's full name (R1).</summary>
    public string Name { get; private set; }

    /// <summary>Gets the contact phone number (R1).</summary>
    public string Phone { get; private set; }

    /// <summary>Gets the contact email address (R1).</summary>
    public string Email { get; private set; }

    /// <summary>Gets the date the staff member (most recently) joined.</summary>
    public DateOnly JoinedOn { get; internal set; }

    /// <summary>Gets the date the staff member left, while inactive.</summary>
    public DateOnly? LeftOn { get; internal set; }

    /// <summary>Gets whether the staff member may receive new allocations (R2).</summary>
    public bool IsActive { get; internal set; }

    /// <summary>Gets the availability owned by this staff member (R3, composition).</summary>
    public DeliveryStaffAvailability Availability { get; }

    /// <summary>Gets deliveries allocated and not yet completed (R5).</summary>
    public IEnumerable<Delivery> CurrentDeliveries =>
        _deliveries.Where(delivery => delivery.Status is DeliveryStatus.Allocated or DeliveryStatus.PickedUp);

    /// <summary>Gets every delivery this staff member performed or holds, newest first (R7).</summary>
    public IEnumerable<Delivery> DeliveryHistory => _deliveries.OrderByDescending(delivery => delivery.Window.Start);

    /// <summary>Changes the staff member's contact details (A1 T10 subtask 3).</summary>
    public void UpdateDetails(string name, string phone, string email)
    {
        string validName = Require.PersonName(name, "Staff name", 100);
        string validPhone = Require.Phone(phone);
        string validEmail = Require.Email(email);
        Name = validName;
        Phone = validPhone;
        Email = validEmail;
    }

    /// <summary>Finds a current allocation whose window overlaps <paramref name="window"/>.</summary>
    /// <param name="window">Window being considered.</param>
    /// <param name="ignoring">A delivery to leave out, typically the one being reallocated.</param>
    public Delivery? FindConflictingAllocation(DeliveryWindow window, Delivery? ignoring)
    {
        return CurrentDeliveries.FirstOrDefault(
            delivery => !ReferenceEquals(delivery, ignoring) && delivery.Window.Overlaps(window));
    }

    internal void AssignDelivery(Delivery delivery)
    {
        if (!_deliveries.Contains(delivery))
        {
            _deliveries.Add(delivery);
        }
    }

    internal void ReleaseDelivery(Delivery delivery) => _deliveries.Remove(delivery);
}

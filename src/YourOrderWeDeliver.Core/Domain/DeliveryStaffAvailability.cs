using YourOrderWeDeliver.Core.Common;

namespace YourOrderWeDeliver.Core.Domain;

/// <summary>A recorded period during which a staff member can work.</summary>
/// <param name="Id">Identifier, unique for the staff member, for example "DS-001-A3".</param>
/// <param name="Window">Start and end of the period.</param>
public sealed record AvailabilityPeriod(string Id, DeliveryWindow Window);

/// <summary>Observer notified when a staff member's availability changes (A2 Section 6.5.2).</summary>
public interface IAvailabilityObserver
{
    /// <summary>Checks whether a period can be removed before availability changes.</summary>
    /// <param name="availability">The availability being changed.</param>
    /// <param name="period">The period proposed for removal.</param>
    void ValidatePeriodRemoval(
        DeliveryStaffAvailability availability, AvailabilityPeriod period);
    /// <summary>Called after a period is added or removed.</summary>
    /// <param name="availability">The availability that changed.</param>
    /// <param name="changedAt">When the change was recorded.</param>
    void OnAvailabilityChanged(DeliveryStaffAvailability availability, DateTime changedAt);
}

/// <summary>
/// The availability periods of one <see cref="DeliveryStaff"/> member (A2 CRC 5.8) and the
/// Subject of the Observer pattern. It answers "is this staff member working at that time?"
/// but knows nothing about deliveries; reacting to a change is the observers' job.
/// </summary>
public sealed class DeliveryStaffAvailability
{
    /// <summary>Longest single availability period that may be recorded.</summary>
    public static readonly TimeSpan MaxPeriodLength = TimeSpan.FromHours(14);

    private readonly List<AvailabilityPeriod> _periods = [];
    private readonly List<IAvailabilityObserver> _observers = [];
    private int _lastPeriodNumber;

    internal DeliveryStaffAvailability(DeliveryStaff staff)
    {
        Staff = staff;
    }

    /// <summary>Gets the staff member whose availability this is (R1).</summary>
    public DeliveryStaff Staff { get; }

    /// <summary>Gets the recorded periods in time order (R2).</summary>
    public IReadOnlyList<AvailabilityPeriod> Periods => _periods;

    /// <summary>Records a new availability period (R3) and notifies observers.</summary>
    public AvailabilityPeriod AddPeriod(DateTime start, DateTime end, DateTime now)
    {
        Require.That(end > start, "The availability end time must be later than the start time.");
        var window = new DeliveryWindow(start, end);
        Require.That(
            window.Duration <= MaxPeriodLength,
            $"An availability period can be at most {MaxPeriodLength.TotalHours} hours long. Record longer availability as separate periods.");
        Require.That(end > now, "The availability period has already ended. Enter a period that finishes in the future.");

        AvailabilityPeriod? overlap = _periods.FirstOrDefault(period => period.Window.Overlaps(window));
        if (overlap is not null)
        {
            throw new BusinessRuleException(
                $"This period overlaps existing availability {DisplayFormat.Window(overlap.Window)}. "
                + "Remove that period first or choose different times.");
        }

        var added = new AvailabilityPeriod($"{Staff.Id}-A{++_lastPeriodNumber}", window);
        Insert(added);
        NotifyObservers(now);
        return added;
    }

    /// <summary>Removes a period, for example when the staff member becomes unavailable (R3).</summary>
    public void RemovePeriod(string periodId, DateTime now)
    {
        AvailabilityPeriod period = _periods.FirstOrDefault(p => string.Equals(p.Id, periodId, StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException($"Availability period {periodId} was not found for {Staff.Name}.");
        foreach (IAvailabilityObserver observer in _observers.ToList())
        {
            observer.ValidatePeriodRemoval(this, period);
        }
        _periods.Remove(period);
        NotifyObservers(now);
    }

    /// <summary>Determines whether one recorded period covers the whole window (R4).</summary>
    public bool IsAvailableFor(DeliveryWindow window) => _periods.Any(period => period.Window.Contains(window));

    /// <summary>Registers an observer for availability changes (R6).</summary>
    public void Subscribe(IAvailabilityObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        if (!_observers.Contains(observer))
        {
            _observers.Add(observer);
        }
    }

    /// <summary>Removes a previously registered observer.</summary>
    public void Unsubscribe(IAvailabilityObserver observer) => _observers.Remove(observer);

    internal void RestorePeriod(AvailabilityPeriod period)
    {
        Insert(period);
        string suffix = period.Id[(period.Id.LastIndexOf('A') + 1)..];
        if (int.TryParse(suffix, out int number) && number > _lastPeriodNumber)
        {
            _lastPeriodNumber = number;
        }
    }

    private void Insert(AvailabilityPeriod period)
    {
        _periods.Add(period);
        _periods.Sort((left, right) => left.Window.Start.CompareTo(right.Window.Start));
    }

    private void NotifyObservers(DateTime changedAt)
    {
        foreach (IAvailabilityObserver observer in _observers.ToList())
        {
            observer.OnAvailabilityChanged(this, changedAt);
        }
    }
}

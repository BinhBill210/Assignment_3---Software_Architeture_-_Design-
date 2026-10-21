using YourOrderWeDeliver.Core.Common;

namespace YourOrderWeDeliver.Core.Domain;

/// <summary>A period of time with a start strictly before its end.</summary>
public readonly record struct DeliveryWindow
{
    /// <summary>Creates a window.</summary>
    /// <exception cref="BusinessRuleException">The end is not later than the start.</exception>
    public DeliveryWindow(DateTime start, DateTime end)
    {
        Require.That(end > start, "The end time must be later than the start time.");
        Start = start;
        End = end;
    }

    /// <summary>Gets the start of the window.</summary>
    public DateTime Start { get; }

    /// <summary>Gets the end of the window.</summary>
    public DateTime End { get; }

    /// <summary>Gets the length of the window.</summary>
    public TimeSpan Duration => End - Start;

    /// <summary>Determines whether this window shares any time with <paramref name="other"/>.</summary>
    public bool Overlaps(DeliveryWindow other) => Start < other.End && other.Start < End;

    /// <summary>Determines whether <paramref name="other"/> lies completely inside this window.</summary>
    public bool Contains(DeliveryWindow other) => Start <= other.Start && other.End <= End;

    /// <inheritdoc />
    public override string ToString() => DisplayFormat.Window(this);
}

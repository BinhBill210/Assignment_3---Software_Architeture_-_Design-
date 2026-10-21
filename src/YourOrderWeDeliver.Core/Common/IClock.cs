namespace YourOrderWeDeliver.Core.Common;

/// <summary>Supplies the current local time so that time-dependent rules can be tested.</summary>
public interface IClock
{
    /// <summary>Gets the current local date and time.</summary>
    DateTime Now { get; }
}

/// <summary>Clock backed by the operating system time.</summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTime Now => DateTime.Now;
}

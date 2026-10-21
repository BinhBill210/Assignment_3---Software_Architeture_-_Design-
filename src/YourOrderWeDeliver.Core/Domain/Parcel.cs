using YourOrderWeDeliver.Core.Common;

namespace YourOrderWeDeliver.Core.Domain;

/// <summary>
/// An item to be collected and transported (A2 CRC 5.5). A parcel exists from the moment its
/// request is recorded and keeps its own collected and delivered state.
/// </summary>
public sealed class Parcel
{
    /// <summary>Lightest weight accepted, in kilograms.</summary>
    public const decimal MinWeightKg = 0.1m;

    /// <summary>Heaviest weight one delivery staff member may carry, in kilograms.</summary>
    public const decimal MaxWeightKg = 30m;

    internal Parcel(string id, string? description, decimal weightKg, bool isFragile, int number)
    {
        Id = id;
        Description = Require.Text(description, $"Parcel {number} description", 100);
        Require.That(
            weightKg >= MinWeightKg && weightKg <= MaxWeightKg,
            $"Parcel {number} weight must be between {MinWeightKg} kg and {MaxWeightKg} kg.");
        WeightKg = decimal.Round(weightKg, 2);
        IsFragile = isFragile;
    }

    /// <summary>Gets the identifier, for example "REQ-100001-P1".</summary>
    public string Id { get; }

    /// <summary>Gets the description of the contents.</summary>
    public string Description { get; }

    /// <summary>Gets the weight in kilograms.</summary>
    public decimal WeightKg { get; }

    /// <summary>Gets whether the parcel needs careful handling.</summary>
    public bool IsFragile { get; }

    /// <summary>Gets the request this parcel belongs to (R1).</summary>
    public DeliveryRequest Request { get; internal set; } = null!;

    /// <summary>Gets the operational delivery once the quote is accepted (R2).</summary>
    public Delivery? Delivery { get; internal set; }

    /// <summary>Gets when the parcel was collected, if it has been.</summary>
    public DateTime? CollectedAt { get; internal set; }

    /// <summary>Gets when the parcel was handed over, if it has been.</summary>
    public DateTime? DeliveredAt { get; internal set; }

    /// <summary>Gets whether the parcel has been collected (R6).</summary>
    public bool IsCollected => CollectedAt.HasValue;

    /// <summary>Gets whether the parcel has been delivered (R7).</summary>
    public bool IsDelivered => DeliveredAt.HasValue;

    internal void MarkCollected(DateTime at)
    {
        Require.That(!IsCollected, $"Parcel {Id} is already marked as collected.");
        CollectedAt = at;
    }

    internal void UndoCollected()
    {
        Require.That(IsCollected, $"Parcel {Id} is not marked as collected.");
        CollectedAt = null;
    }

    internal void MarkDelivered(DateTime at)
    {
        Require.That(IsCollected, $"Parcel {Id} has not been collected, so it cannot be recorded as delivered.");
        Require.That(!IsDelivered, $"Parcel {Id} is already marked as delivered.");
        DeliveredAt = at;
    }

    internal void UndoDelivered()
    {
        Require.That(IsDelivered, $"Parcel {Id} is not marked as delivered.");
        DeliveredAt = null;
    }
}

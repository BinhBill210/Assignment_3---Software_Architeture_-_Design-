using YourOrderWeDeliver.Core.Domain;

namespace YourOrderWeDeliver.Core.Pricing;

/// <summary>
/// Strategy for suggesting a delivery cost. Office staff remain responsible for the final quoted
/// cost (A1 T2 subtask 2), so a policy only produces a starting figure.
/// </summary>
public interface IPricingPolicy
{
    /// <summary>Gets a short description of how the suggestion is calculated.</summary>
    string Description { get; }

    /// <summary>Calculates the suggested cost for the request.</summary>
    decimal CalculateCost(DeliveryRequest request);
}

/// <summary>
/// Default pricing: a base fee covering the first parcel, a fee per additional parcel,
/// a per-kilogram charge and a handling fee for each fragile parcel.
/// </summary>
public sealed class StandardPricingPolicy : IPricingPolicy
{
    /// <summary>Creates the policy with the given rates.</summary>
    public StandardPricingPolicy(decimal baseFee, decimal additionalParcelFee, decimal perKilogram, decimal fragileFee)
    {
        if (baseFee < 0 || additionalParcelFee < 0 || perKilogram < 0 || fragileFee < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(baseFee), "Pricing rates cannot be negative.");
        }

        BaseFee = baseFee;
        AdditionalParcelFee = additionalParcelFee;
        PerKilogram = perKilogram;
        FragileFee = fragileFee;
    }

    /// <summary>Gets the fee that includes the first parcel.</summary>
    public decimal BaseFee { get; }

    /// <summary>Gets the fee for each parcel after the first.</summary>
    public decimal AdditionalParcelFee { get; }

    /// <summary>Gets the charge per kilogram of total weight.</summary>
    public decimal PerKilogram { get; }

    /// <summary>Gets the handling fee for each fragile parcel.</summary>
    public decimal FragileFee { get; }

    /// <inheritdoc />
    public string Description =>
        $"Base {BaseFee:0.00} + {AdditionalParcelFee:0.00} per extra parcel + {PerKilogram:0.00}/kg + {FragileFee:0.00} per fragile parcel";

    /// <summary>Creates the policy with the rates agreed for the prototype.</summary>
    public static StandardPricingPolicy Default() => new(15.00m, 5.00m, 1.50m, 4.00m);

    /// <inheritdoc />
    public decimal CalculateCost(DeliveryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        int parcelCount = request.Parcels.Count;
        int fragileCount = request.Parcels.Count(parcel => parcel.IsFragile);
        decimal cost = BaseFee
            + (Math.Max(0, parcelCount - 1) * AdditionalParcelFee)
            + (request.TotalWeightKg * PerKilogram)
            + (fragileCount * FragileFee);
        return decimal.Round(cost, 2, MidpointRounding.AwayFromZero);
    }
}

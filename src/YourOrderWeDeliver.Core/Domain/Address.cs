using YourOrderWeDeliver.Core.Common;

namespace YourOrderWeDeliver.Core.Domain;

/// <summary>Validated street address used for pickup and destination information.</summary>
public sealed record Address
{
    private Address(string street, string suburb, string postcode)
    {
        Street = street;
        Suburb = suburb;
        Postcode = postcode;
    }

    /// <summary>Gets the street number and name.</summary>
    public string Street { get; }

    /// <summary>Gets the suburb.</summary>
    public string Suburb { get; }

    /// <summary>Gets the four-digit Australian postcode.</summary>
    public string Postcode { get; }

    /// <summary>Validates raw input and creates an address.</summary>
    /// <param name="street">Street number and name.</param>
    /// <param name="suburb">Suburb name.</param>
    /// <param name="postcode">Four-digit postcode.</param>
    /// <param name="label">Prefix used in error messages, for example "Pickup".</param>
    public static Address Create(string? street, string? suburb, string? postcode, string label)
    {
        string validStreet = Require.Text(street, $"{label} street address", 120);
        string validSuburb = Require.Text(suburb, $"{label} suburb", 60);
        string validPostcode = Require.Text(postcode, $"{label} postcode", 10);
        Require.That(
            validPostcode.Length == 4 && validPostcode.All(char.IsAsciiDigit),
            $"{label} postcode must be exactly four digits, for example 3122.");
        return new Address(validStreet, validSuburb, validPostcode);
    }

    /// <summary>Determines whether two addresses describe the same place, ignoring letter case.</summary>
    public bool IsSamePlaceAs(Address other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return string.Equals(ToString(), other.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public override string ToString() => $"{Street}, {Suburb} {Postcode}";
}

using System.Globalization;
using YourOrderWeDeliver.Core.Common;

namespace YourOrderWeDeliver.Core.Domain;

/// <summary>An inclusive range of postcodes.</summary>
/// <param name="From">First postcode in the range.</param>
/// <param name="To">Last postcode in the range.</param>
public readonly record struct PostcodeRange(int From, int To)
{
    /// <summary>Parses "3000-3207" or a single postcode such as "3335".</summary>
    public static PostcodeRange Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        string[] parts = text.Split('-', StringSplitOptions.TrimEntries);
        int from = int.Parse(parts[0], CultureInfo.InvariantCulture);
        int to = parts.Length > 1 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : from;
        if (to < from)
        {
            throw new FormatException($"Postcode range '{text}' ends before it starts.");
        }

        return new PostcodeRange(from, to);
    }

    /// <summary>Determines whether the postcode falls inside the range.</summary>
    public bool Contains(int postcode) => postcode >= From && postcode <= To;
}

/// <summary>
/// The geographic area the business currently serves. It is configuration rather than code so that
/// a new service area can be added without an application change (A1 QR5.2).
/// </summary>
public sealed class ServiceArea
{
    private static readonly string[] GreaterMelbourneRanges =
        ["3000-3211", "3335-3341", "3427-3442", "3750-3811", "3910-3944", "3975-3981", "8000-8999"];

    /// <summary>Creates a service area.</summary>
    public ServiceArea(string name, IEnumerable<PostcodeRange> postcodeRanges)
    {
        Name = Require.Text(name, "Service area name", 80);
        PostcodeRanges = postcodeRanges?.ToList() ?? throw new ArgumentNullException(nameof(postcodeRanges));
        if (PostcodeRanges.Count == 0)
        {
            throw new ArgumentException("A service area needs at least one postcode range.", nameof(postcodeRanges));
        }
    }

    /// <summary>Gets the display name, for example "Greater Melbourne".</summary>
    public string Name { get; }

    /// <summary>Gets the postcode ranges covered.</summary>
    public IReadOnlyList<PostcodeRange> PostcodeRanges { get; }

    /// <summary>Approximate Greater Melbourne metropolitan postcodes, used when no configuration is supplied.</summary>
    public static ServiceArea GreaterMelbourne()
    {
        return new ServiceArea("Greater Melbourne", GreaterMelbourneRanges.Select(PostcodeRange.Parse));
    }

    /// <summary>Determines whether the address lies inside the service area.</summary>
    public bool Covers(Address address)
    {
        ArgumentNullException.ThrowIfNull(address);
        int postcode = int.Parse(address.Postcode, CultureInfo.InvariantCulture);
        return PostcodeRanges.Any(range => range.Contains(postcode));
    }
}

using System.ComponentModel.DataAnnotations;
using YourOrderWeDeliver.Core.Domain;

namespace YourOrderWeDeliver.Web.Models;

/// <summary>
/// Address fields of a form. Annotations give immediate field-level feedback; the domain classes
/// still apply the authoritative rules (for example the service-area check).
/// </summary>
public sealed class AddressInput
{
    /// <summary>Gets or sets the street number and name.</summary>
    [Required(ErrorMessage = "Enter the street number and name.")]
    [StringLength(120, ErrorMessage = "The street address must be 120 characters or fewer.")]
    [Display(Name = "Street address")]
    public string? Street { get; set; }

    /// <summary>Gets or sets the suburb.</summary>
    [Required(ErrorMessage = "Enter the suburb.")]
    [StringLength(60, ErrorMessage = "The suburb must be 60 characters or fewer.")]
    public string? Suburb { get; set; }

    /// <summary>Gets or sets the postcode.</summary>
    [Required(ErrorMessage = "Enter the postcode.")]
    [RegularExpression(@"^\d{4}$", ErrorMessage = "The postcode must be exactly four digits, for example 3122.")]
    public string? Postcode { get; set; }

    /// <summary>Creates the input from an existing address.</summary>
    public static AddressInput From(Address address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return new AddressInput { Street = address.Street, Suburb = address.Suburb, Postcode = address.Postcode };
    }

    /// <summary>Converts to the domain data-holder.</summary>
    public AddressDetails ToDetails() => new(Street, Suburb, Postcode);
}

/// <summary>One parcel row of the request form.</summary>
public sealed class ParcelInput
{
    /// <summary>Gets or sets what the parcel contains.</summary>
    [Required(ErrorMessage = "Describe what the parcel contains.")]
    [StringLength(100, ErrorMessage = "The description must be 100 characters or fewer.")]
    public string? Description { get; set; }

    /// <summary>Gets or sets the weight in kilograms.</summary>
    [Required(ErrorMessage = "Enter the parcel weight in kilograms.")]
    [Range(0.1, 30.0, ErrorMessage = "The weight must be between 0.1 kg and 30 kg.")]
    [Display(Name = "Weight (kg)")]
    public decimal? WeightKg { get; set; }

    /// <summary>Gets or sets whether the parcel is fragile.</summary>
    [Display(Name = "Fragile")]
    public bool IsFragile { get; set; }
}

/// <summary>All fields of the delivery request form (A1 task T1).</summary>
public sealed class RequestInput
{
    /// <summary>Gets or sets the customer's name.</summary>
    [Required(ErrorMessage = "Enter the customer's full name.")]
    [StringLength(100, ErrorMessage = "The name must be 100 characters or fewer.")]
    [Display(Name = "Full name")]
    public string? CustomerName { get; set; }

    /// <summary>Gets or sets the customer's email.</summary>
    [Required(ErrorMessage = "Enter an email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address, for example name@example.com.")]
    [Display(Name = "Email address")]
    public string? CustomerEmail { get; set; }

    /// <summary>Gets or sets the customer's phone number.</summary>
    [Required(ErrorMessage = "Enter a phone number.")]
    [StringLength(20, ErrorMessage = "The phone number is too long.")]
    [Display(Name = "Phone number")]
    public string? CustomerPhone { get; set; }

    /// <summary>Gets or sets the pickup address.</summary>
    public AddressInput Pickup { get; set; } = new();

    /// <summary>Gets or sets the destination address.</summary>
    public AddressInput Destination { get; set; } = new();

    /// <summary>Gets or sets the preferred pickup date.</summary>
    [Required(ErrorMessage = "Choose a preferred pickup date.")]
    [DataType(DataType.Date)]
    [Display(Name = "Preferred pickup date")]
    public DateTime? PreferredPickupDate { get; set; }

    /// <summary>Gets or sets optional instructions.</summary>
    [StringLength(300, ErrorMessage = "Special instructions must be 300 characters or fewer.")]
    [Display(Name = "Special instructions (optional)")]
    public string? SpecialInstructions { get; set; }

    /// <summary>Gets the parcel rows.</summary>
    public IList<ParcelInput> Parcels { get; } = new List<ParcelInput>();

    /// <summary>Creates a blank form with one parcel row.</summary>
    public static RequestInput CreateEmpty(DateOnly today)
    {
        var input = new RequestInput { PreferredPickupDate = today.ToDateTime(TimeOnly.MinValue) };
        input.Parcels.Add(new ParcelInput());
        return input;
    }

    /// <summary>Converts to the domain data-holder.</summary>
    public RequestDetails ToDetails()
    {
        return new RequestDetails(
            CustomerName,
            CustomerEmail,
            CustomerPhone,
            Pickup.ToDetails(),
            Destination.ToDetails(),
            DateOnly.FromDateTime(PreferredPickupDate ?? DateTime.MinValue),
            SpecialInstructions,
            Parcels.Select(parcel => new ParcelDetails(parcel.Description, parcel.WeightKg ?? 0m, parcel.IsFragile)).ToList());
    }
}

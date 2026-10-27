using System.ComponentModel.DataAnnotations;

namespace YourOrderWeDeliver.Web.Pages.Manager.Staff;

/// <summary>Staff contact fields shared by the "new staff" and "edit staff" forms (A1 T10).</summary>
public sealed class StaffInput
{
    /// <summary>Gets or sets the full name.</summary>
    [Required(ErrorMessage = "Enter the staff member's full name.")]
    [StringLength(100, ErrorMessage = "The name must be 100 characters or fewer.")]
    [Display(Name = "Full name")]
    public string? Name { get; set; }

    /// <summary>Gets or sets the phone number.</summary>
    [Required(ErrorMessage = "Enter a phone number.")]
    [StringLength(20, ErrorMessage = "The phone number is too long.")]
    [Display(Name = "Phone number")]
    public string? Phone { get; set; }

    /// <summary>Gets or sets the email address.</summary>
    [Required(ErrorMessage = "Enter an email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address, for example name@example.com.")]
    [Display(Name = "Email address")]
    public string? Email { get; set; }
}

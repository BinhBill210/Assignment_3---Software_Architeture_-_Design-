using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using YourOrderWeDeliver.Core.Coordination;
using YourOrderWeDeliver.Core.Domain;
using YourOrderWeDeliver.Web.Infrastructure;

namespace YourOrderWeDeliver.Web.Pages.Manager.Staff;

/// <summary>A1 task T10 subtasks 1 and 2: review the workforce and record a staff member who joins.</summary>
public sealed class IndexModel(DeliveryManagementSystem system) : DmsPageModel(system)
{
    /// <summary>Gets or sets the new staff member's details.</summary>
    [BindProperty]
    public StaffInput Input { get; set; } = new();

    /// <summary>Gets or sets the joining date.</summary>
    [BindProperty]
    [Required(ErrorMessage = "Enter the date the staff member joins.")]
    [DataType(DataType.Date)]
    [Display(Name = "Joining date")]
    public DateTime? JoinedOn { get; set; }

    /// <summary>Gets every staff record, active first.</summary>
    public IReadOnlyList<DeliveryStaff> StaffList { get; private set; } = [];

    /// <summary>Shows the list and an empty "new staff" form.</summary>
    public void OnGet()
    {
        JoinedOn = Now.Date;
        Load();
    }

    /// <summary>Creates the new staff record.</summary>
    public IActionResult OnPostAdd()
    {
        Load();
        if (!ModelState.IsValid)
        {
            return Page();
        }

        DeliveryStaff? staff = null;
        if (!TryChange(() => staff = Roster.RegisterNewStaff(Input.Name!, Input.Phone!, Input.Email!, DateOnly.FromDateTime(JoinedOn!.Value))))
        {
            return Page();
        }

        StatusMessage = $"{staff!.Name} added as {staff.Id}. They sign in with this staff ID. Add their availability next.";
        return RedirectToPage("Edit", new { id = staff.Id });
    }

    private void Load()
    {
        StaffList = Roster.AllStaff.OrderByDescending(staff => staff.IsActive).ThenBy(staff => staff.Id, StringComparer.Ordinal).ToList();
    }
}

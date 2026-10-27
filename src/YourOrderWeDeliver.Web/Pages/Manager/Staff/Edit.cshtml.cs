using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using YourOrderWeDeliver.Core.Coordination;
using YourOrderWeDeliver.Core.Domain;
using YourOrderWeDeliver.Web.Infrastructure;

namespace YourOrderWeDeliver.Web.Pages.Manager.Staff;

/// <summary>
/// A1 task T10 subtasks 3 and 4 and variant 4a: update a staff member's details, record that they
/// have left (their unpicked deliveries are reassigned), or reactivate the same record when they rejoin.
/// </summary>
public sealed class EditModel(DeliveryManagementSystem system) : DmsPageModel(system)
{
    /// <summary>Gets or sets the staff ID from the route.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Id { get; set; }

    /// <summary>Gets or sets the contact details form.</summary>
    [BindProperty]
    public StaffInput Input { get; set; } = new();

    /// <summary>Gets or sets the leaving or rejoining date.</summary>
    [BindProperty]
    [Required(ErrorMessage = "Enter the date.")]
    [DataType(DataType.Date)]
    [Display(Name = "Date")]
    public DateTime? EffectiveDate { get; set; }

    /// <summary>Gets the staff record.</summary>
    public DeliveryStaff? Record { get; private set; }

    /// <summary>Gets whether the "has left" confirmation is shown.</summary>
    public bool IsConfirmingLeave { get; private set; }

    /// <summary>Gets reassignments made when the staff member was deactivated.</summary>
    public IReadOnlyList<ReassignmentRecord> Outcomes { get; private set; } = [];

    /// <summary>Shows the record.</summary>
    public IActionResult OnGet()
    {
        ModelState.Clear();
        if (!Load())
        {
            return NotFoundMessage($"No staff record '{Id}' was found.");
        }

        Input = new StaffInput { Name = Record!.Name, Phone = Record.Phone, Email = Record.Email };
        EffectiveDate = Now.Date;
        return Page();
    }

    /// <summary>Saves changed contact details.</summary>
    public IActionResult OnPostSave()
    {
        ModelState.Remove(nameof(EffectiveDate));
        if (!Load())
        {
            return NotFoundMessage($"No staff record '{Id}' was found.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        bool saved = TryChange(() => Roster.UpdateStaffDetails(Record!, Input.Name!, Input.Phone!, Input.Email!), "Staff details updated.");
        return saved ? RedirectToPage(new { id = Id }) : Page();
    }

    /// <summary>Asks for confirmation before recording that the staff member has left.</summary>
    public IActionResult OnPostConfirmLeave()
    {
        KeepOnlyDateValidation();
        if (!Load())
        {
            return NotFoundMessage($"No staff record '{Id}' was found.");
        }

        IsConfirmingLeave = ModelState.IsValid;
        return Page();
    }

    /// <summary>Records that the staff member has left; allocations not yet picked up are reassigned.</summary>
    public IActionResult OnPostLeave()
    {
        KeepOnlyDateValidation();
        if (!Load())
        {
            return NotFoundMessage($"No staff record '{Id}' was found.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        IReadOnlyList<ReassignmentRecord> outcomes = [];
        if (!TryChange(() => outcomes = Roster.DeactivateStaff(Record!, DateOnly.FromDateTime(EffectiveDate!.Value), Now)))
        {
            return Page();
        }

        Outcomes = outcomes;
        ViewData["Outcome"] = $"{Record!.Name} is now inactive and cannot receive new deliveries. Their history is kept.";
        return Page();
    }

    /// <summary>Cancels the "has left" confirmation.</summary>
    public IActionResult OnPostCancel() => RedirectToPage(new { id = Id });

    /// <summary>Reactivates the existing record for a staff member who rejoins.</summary>
    public IActionResult OnPostReactivate()
    {
        KeepOnlyDateValidation();
        if (!Load())
        {
            return NotFoundMessage($"No staff record '{Id}' was found.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        bool saved = TryChange(
            () => Roster.ReactivateStaff(Record!, DateOnly.FromDateTime(EffectiveDate!.Value)),
            $"{Record!.Name} has been reactivated. Add their availability so they can be scheduled.");
        return saved ? RedirectToPage(new { id = Id }) : Page();
    }

    private bool Load()
    {
        Record = Roster.Find(Id);
        return Record is not null;
    }

    private void KeepOnlyDateValidation()
    {
        foreach (string key in ModelState.Keys.Where(key => key.Length > 0 && key != nameof(EffectiveDate)).ToList())
        {
            ModelState.Remove(key);
        }
    }
}

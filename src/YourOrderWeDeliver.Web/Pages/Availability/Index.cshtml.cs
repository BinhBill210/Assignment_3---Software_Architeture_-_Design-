using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using YourOrderWeDeliver.Core.Common;
using YourOrderWeDeliver.Core.Coordination;
using YourOrderWeDeliver.Core.Domain;
using YourOrderWeDeliver.Web.Infrastructure;

namespace YourOrderWeDeliver.Web.Pages.Availability;

/// <summary>
/// A1 task T5: maintain delivery staff availability. Office staff and managers can choose any
/// active staff member; delivery staff can only change their own (A1 assumption A5). Changes are
/// observed by <see cref="StaffRoster"/>, which reassigns affected deliveries (A2 Scenario 4).
/// </summary>
public sealed class IndexModel(DeliveryManagementSystem system) : DmsPageModel(system)
{
    /// <summary>Gets or sets the staff member whose availability is shown (office users only).</summary>
    [BindProperty(SupportsGet = true)]
    public string? StaffId { get; set; }

    /// <summary>Gets or sets the new period form.</summary>
    [BindProperty]
    public PeriodInput Period { get; set; } = new();

    /// <summary>Gets or sets the period selected for removal.</summary>
    [BindProperty]
    public string? PeriodId { get; set; }

    /// <summary>Gets the staff member whose availability is shown.</summary>
    public DeliveryStaff? Staff { get; private set; }

    /// <summary>Gets the staff choices for office users.</summary>
    public IReadOnlyList<SelectListItem> StaffChoices { get; private set; } = [];

    /// <summary>Gets the period waiting for removal confirmation.</summary>
    public AvailabilityPeriod? PendingRemoval { get; private set; }

    /// <summary>Gets the allocations affected by the period waiting for removal.</summary>
    public IReadOnlyList<Delivery> AffectedByRemoval { get; private set; } = [];

    /// <summary>Gets reassignments caused by the last change.</summary>
    public IReadOnlyList<ReassignmentRecord> Outcomes { get; private set; } = [];

    /// <summary>Gets whether the user may choose another staff member.</summary>
    public bool CanChooseStaff => User.IsOffice();

    /// <summary>Shows the selected staff member's availability with a blank period form.</summary>
    public IActionResult OnGet()
    {
        ModelState.Clear();
        if (!Load())
        {
            return Page();
        }

        DateTime tomorrow = Now.Date.AddDays(1);
        Period = new PeriodInput { Start = tomorrow.AddHours(9), End = tomorrow.AddHours(17) };
        return Page();
    }

    /// <summary>Records a new availability period.</summary>
    public IActionResult OnPostAdd()
    {
        RemoveValidationExcept(nameof(Period));
        if (!Load() || !ModelState.IsValid)
        {
            return Page();
        }

        AvailabilityPeriod? added = null;
        if (!TryChange(() => added = Staff!.Availability.AddPeriod(Period.Start!.Value, Period.End!.Value, Now)))
        {
            return Page();
        }

        StatusMessage = $"Availability added for {Staff!.Name}: {DisplayFormat.Window(added!.Window)}.";
        return RedirectToPage(new { staffId = Staff.Id });
    }

    /// <summary>Shows the removal confirmation, including any deliveries that will be reassigned.</summary>
    public IActionResult OnPostConfirmRemove()
    {
        ModelState.Clear();
        if (!Load())
        {
            return Page();
        }

        PendingRemoval = Staff!.Availability.Periods.FirstOrDefault(period => period.Id == PeriodId);
        if (PendingRemoval is null)
        {
            ModelState.AddModelError(string.Empty, "That availability period no longer exists. It may already have been removed.");
            return Page();
        }

        AffectedByRemoval = Staff.CurrentDeliveries
            .Where(delivery => delivery.Status == DeliveryStatus.Allocated && PendingRemoval.Window.Overlaps(delivery.Window))
            .ToList();
        return Page();
    }

    /// <summary>Removes the confirmed period; the roster reacts to the change.</summary>
    public IActionResult OnPostRemove()
    {
        ModelState.Clear();
        if (!Load())
        {
            return Page();
        }

        int reassignmentsBefore = Roster.ReassignmentLog.Count;
        if (!TryChange(() => Staff!.Availability.RemovePeriod(PeriodId ?? string.Empty, Now)))
        {
            return Page();
        }

        Outcomes = Roster.ReassignmentLog.Take(Roster.ReassignmentLog.Count - reassignmentsBefore).ToList();
        if (Outcomes.Count == 0)
        {
            StatusMessage = "Availability period removed. No allocated deliveries were affected.";
            return RedirectToPage(new { staffId = Staff!.Id });
        }

        ViewData["Outcome"] = "Availability period removed. Allocations that no longer fit were reviewed automatically:";
        return Page();
    }

    /// <summary>Returns to the list without removing anything.</summary>
    public IActionResult OnPostCancel() => RedirectToPage(new { staffId = StaffId });

    private bool Load()
    {
        if (CanChooseStaff)
        {
            StaffChoices = Roster.ActiveStaff
                .Select(staff => new SelectListItem($"{staff.Name} ({staff.Id})", staff.Id))
                .ToList();
            StaffId ??= Roster.ActiveStaff.FirstOrDefault()?.Id;
        }
        else
        {
            StaffId = User.StaffId();
        }

        Staff = Roster.Find(StaffId);
        if (Staff is null || !Staff.IsActive)
        {
            Staff = null;
            ModelState.AddModelError(string.Empty, "Choose an active delivery staff member.");
            return false;
        }

        return true;
    }

    private void RemoveValidationExcept(string prefix)
    {
        foreach (string key in ModelState.Keys.Where(key => key.Length > 0 && !key.StartsWith(prefix + ".", StringComparison.Ordinal)).ToList())
        {
            ModelState.Remove(key);
        }
    }

    /// <summary>New availability period fields.</summary>
    public sealed class PeriodInput
    {
        /// <summary>Gets or sets when the staff member becomes available.</summary>
        [Required(ErrorMessage = "Enter the date and time the availability starts.")]
        [Display(Name = "Available from")]
        public DateTime? Start { get; set; }

        /// <summary>Gets or sets when the availability ends.</summary>
        [Required(ErrorMessage = "Enter the date and time the availability ends.")]
        [Display(Name = "Available until")]
        public DateTime? End { get; set; }
    }
}

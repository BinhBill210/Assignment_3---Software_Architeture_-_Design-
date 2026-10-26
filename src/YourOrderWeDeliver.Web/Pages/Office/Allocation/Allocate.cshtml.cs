using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using YourOrderWeDeliver.Core.Coordination;
using YourOrderWeDeliver.Core.Domain;
using YourOrderWeDeliver.Web.Infrastructure;

namespace YourOrderWeDeliver.Web.Pages.Office.Allocation;

/// <summary>Data-holder: one staff member as seen when choosing who to allocate.</summary>
/// <param name="Staff">Staff member.</param>
/// <param name="Unsuitability">Why they cannot take the delivery, or <c>null</c> when they can.</param>
public sealed record StaffOption(DeliveryStaff Staff, string? Unsuitability)
{
    /// <summary>Gets whether the staff member may be selected.</summary>
    public bool IsSuitable => Unsuitability is null;
}

/// <summary>
/// A1 task T4 subtasks 2 to 5 (and variant 3a): review the delivery and staff availability,
/// select a suitable staff member and confirm the allocation or reassignment.
/// </summary>
public sealed class AllocateModel(DeliveryManagementSystem system) : DmsPageModel(system)
{
    /// <summary>Gets or sets the delivery number from the route.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Id { get; set; }

    /// <summary>Gets or sets the staff member selected.</summary>
    [BindProperty]
    [Required(ErrorMessage = "Select a delivery staff member.")]
    [Display(Name = "Delivery staff member")]
    public string? StaffId { get; set; }

    /// <summary>Gets the delivery being allocated.</summary>
    public Delivery? Delivery { get; private set; }

    /// <summary>Gets every active staff member with their suitability, suitable first.</summary>
    public IReadOnlyList<StaffOption> Options { get; private set; } = [];

    /// <summary>Gets whether the confirmation step is shown.</summary>
    public bool IsConfirming { get; private set; }

    /// <summary>Gets the staff member chosen for confirmation.</summary>
    public DeliveryStaff? Chosen { get; private set; }

    /// <summary>Gets whether the allocation can still be changed.</summary>
    public bool CanAllocate => Delivery?.Status is DeliveryStatus.AwaitingAllocation or DeliveryStatus.Allocated;

    /// <summary>Shows the delivery and the staff options.</summary>
    public IActionResult OnGet()
    {
        ModelState.Clear();
        return Load() ? Page() : NotFoundMessage($"No delivery '{Id}' was found.");
    }

    /// <summary>Shows the confirmation step for the selected staff member.</summary>
    public IActionResult OnPostChoose()
    {
        if (!Load())
        {
            return NotFoundMessage($"No delivery '{Id}' was found.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        StaffOption? option = Options.FirstOrDefault(candidate => candidate.Staff.Id == StaffId);
        if (option is null)
        {
            ModelState.AddModelError(nameof(StaffId), "Select a staff member from the list.");
            return Page();
        }

        if (!option.IsSuitable)
        {
            ModelState.AddModelError(string.Empty, $"{option.Staff.Name} cannot take this delivery because {option.Unsuitability}");
            return Page();
        }

        Chosen = option.Staff;
        IsConfirming = true;
        return Page();
    }

    /// <summary>Returns to the staff list without allocating (change of mind).</summary>
    public IActionResult OnPostCancel()
    {
        ModelState.Clear();
        return Load() ? Page() : NotFoundMessage($"No delivery '{Id}' was found.");
    }

    /// <summary>Records the allocation (subtask 5); the staff member then sees it under "My deliveries" (subtask 6).</summary>
    public IActionResult OnPostConfirm()
    {
        if (!Load())
        {
            return NotFoundMessage($"No delivery '{Id}' was found.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        DeliveryStaff? staff = Roster.Find(StaffId);
        if (staff is null)
        {
            ModelState.AddModelError(nameof(StaffId), "Select a staff member from the list.");
            return Page();
        }

        bool reassigning = Delivery!.AllocatedStaff is not null;
        if (!TryChange(() => Delivery.AllocateStaff(staff, Roster, Now)))
        {
            Options = BuildOptions();
            return Page();
        }

        StatusMessage = reassigning
            ? $"Delivery {Delivery.Id} reassigned to {staff.Name}."
            : $"Delivery {Delivery.Id} allocated to {staff.Name}. It now appears in their delivery list.";
        return RedirectToPage("Index");
    }

    private bool Load()
    {
        Delivery = Register.FindDelivery(Id);
        if (Delivery is null)
        {
            return false;
        }

        Options = BuildOptions();
        return true;
    }

    private List<StaffOption> BuildOptions()
    {
        return Roster.ActiveStaff
            .Where(staff => !ReferenceEquals(staff, Delivery!.AllocatedStaff))
            .Select(staff => new StaffOption(staff, Roster.DescribeUnsuitability(staff, Delivery!.Window, Delivery)))
            .OrderBy(option => option.IsSuitable ? 0 : 1)
            .ThenBy(option => option.Staff.CurrentDeliveries.Count())
            .ThenBy(option => option.Staff.Name, StringComparer.CurrentCulture)
            .ToList();
    }
}

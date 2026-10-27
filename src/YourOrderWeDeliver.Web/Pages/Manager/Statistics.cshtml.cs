using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using YourOrderWeDeliver.Core.Coordination;
using YourOrderWeDeliver.Core.Domain;
using YourOrderWeDeliver.Web.Infrastructure;

namespace YourOrderWeDeliver.Web.Pages.Manager;

/// <summary>
/// A1 task T9: delivery statistics for a day, week, month, year or year to date, for the whole
/// business or one staff member (including staff who have left).
/// </summary>
public sealed class StatisticsModel(DeliveryManagementSystem system) : DmsPageModel(system)
{
    /// <summary>Gets or sets the reporting period.</summary>
    [BindProperty(SupportsGet = true)]
    [Required(ErrorMessage = "Choose a reporting period.")]
    [Display(Name = "Reporting period")]
    public ReportingPeriod? Period { get; set; }

    /// <summary>Gets or sets a day inside the period.</summary>
    [BindProperty(SupportsGet = true)]
    [Required(ErrorMessage = "Choose a date inside the period.")]
    [DataType(DataType.Date)]
    [Display(Name = "Date in period")]
    public DateTime? Date { get; set; }

    /// <summary>Gets or sets the staff member to report on; empty for the whole business.</summary>
    [BindProperty(SupportsGet = true)]
    [Display(Name = "Delivery staff member")]
    public string? StaffId { get; set; }

    /// <summary>Gets the calculated statistics.</summary>
    public DeliveryStatistics? Result { get; private set; }

    /// <summary>Gets the staff choices, active and inactive.</summary>
    public IReadOnlyList<SelectListItem> StaffChoices { get; private set; } = [];

    /// <summary>Calculates the statistics for the chosen criteria.</summary>
    public void OnGet()
    {
        StaffChoices = Roster.AllStaff
            .Select(staff => new SelectListItem($"{staff.Name} ({staff.Id}){(staff.IsActive ? string.Empty : " - left")}", staff.Id))
            .Prepend(new SelectListItem("Whole business", string.Empty))
            .ToList();

        bool firstVisit = Period is null && Date is null && StaffId is null;
        if (firstVisit)
        {
            ModelState.Clear();
            Period = ReportingPeriod.Month;
            Date = Now.Date;
        }

        if (!ModelState.IsValid)
        {
            return;
        }

        DeliveryStaff? staff = null;
        if (!string.IsNullOrWhiteSpace(StaffId))
        {
            staff = Roster.Find(StaffId);
            if (staff is null)
            {
                ModelState.AddModelError(nameof(StaffId), "Choose a staff member from the list.");
                return;
            }
        }

        if (Date!.Value.Date > Now.Date)
        {
            ModelState.AddModelError(nameof(Date), "The date cannot be in the future.");
            return;
        }

        Result = DeliveryStatistics.Calculate(Register, Period!.Value, DateOnly.FromDateTime(Date.Value), staff);
    }
}

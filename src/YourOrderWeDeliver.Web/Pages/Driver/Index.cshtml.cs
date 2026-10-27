using YourOrderWeDeliver.Core.Coordination;
using YourOrderWeDeliver.Core.Domain;
using YourOrderWeDeliver.Web.Infrastructure;

namespace YourOrderWeDeliver.Web.Pages.Driver;

/// <summary>A1 task T4 subtask 6: a delivery staff member sees the deliveries allocated to them.</summary>
public sealed class IndexModel(DeliveryManagementSystem system) : DmsPageModel(system)
{
    /// <summary>Gets the signed-in staff member.</summary>
    public DeliveryStaff? Staff { get; private set; }

    /// <summary>Gets deliveries to pick up or complete, earliest first.</summary>
    public IReadOnlyList<Delivery> Current { get; private set; } = [];

    /// <summary>Gets recently completed deliveries.</summary>
    public IReadOnlyList<Delivery> Completed { get; private set; } = [];

    /// <summary>Loads the lists.</summary>
    public void OnGet()
    {
        Staff = Roster.Find(User.StaffId());
        if (Staff is null)
        {
            return;
        }

        Current = Staff.CurrentDeliveries.OrderBy(delivery => delivery.Window.Start).ToList();
        Completed = Staff.DeliveryHistory.Where(delivery => delivery.Status == DeliveryStatus.Delivered).Take(10).ToList();
    }
}

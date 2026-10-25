using YourOrderWeDeliver.Core.Coordination;
using YourOrderWeDeliver.Core.Domain;
using YourOrderWeDeliver.Web.Infrastructure;

namespace YourOrderWeDeliver.Web.Pages;

/// <summary>Home screen listing the activities available to the current user.</summary>
public sealed class IndexModel(DeliveryManagementSystem system) : DmsPageModel(system)
{
    /// <summary>Gets requests waiting for a quote.</summary>
    public int RequestsAwaitingQuote { get; private set; }

    /// <summary>Gets deliveries waiting for allocation.</summary>
    public int DeliveriesAwaitingAllocation { get; private set; }

    /// <summary>Gets the signed-in delivery staff member's current deliveries.</summary>
    public int MyCurrentDeliveries { get; private set; }

    /// <summary>Gets the active staff count.</summary>
    public int ActiveStaff { get; private set; }

    /// <summary>Loads the summary counts shown to staff.</summary>
    public void OnGet()
    {
        RequestsAwaitingQuote = Register.Requests.Count(request => request.Status == RequestStatus.Submitted);
        DeliveriesAwaitingAllocation = Register.DeliveriesAwaitingAllocation.Count();
        ActiveStaff = Roster.ActiveStaff.Count();
        MyCurrentDeliveries = Roster.Find(User.StaffId())?.CurrentDeliveries.Count() ?? 0;
    }
}

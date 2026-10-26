using YourOrderWeDeliver.Core.Coordination;
using YourOrderWeDeliver.Core.Domain;
using YourOrderWeDeliver.Web.Infrastructure;

namespace YourOrderWeDeliver.Web.Pages.Office.Allocation;

/// <summary>A1 task T4 subtask 1: deliveries that need a staff member, and those that may still be reassigned.</summary>
public sealed class IndexModel(DeliveryManagementSystem system) : DmsPageModel(system)
{
    /// <summary>Gets accepted deliveries with no staff member allocated.</summary>
    public IReadOnlyList<Delivery> AwaitingAllocation { get; private set; } = [];

    /// <summary>Gets allocated deliveries not yet picked up.</summary>
    public IReadOnlyList<Delivery> AllocatedNotPickedUp { get; private set; } = [];

    /// <summary>Gets recent automatic reassignments after staff changes.</summary>
    public IReadOnlyList<ReassignmentRecord> RecentReassignments { get; private set; } = [];

    /// <summary>Loads the lists.</summary>
    public void OnGet()
    {
        AwaitingAllocation = Register.DeliveriesAwaitingAllocation.ToList();
        AllocatedNotPickedUp = Register.DeliveriesAllocatedNotPickedUp.ToList();
        RecentReassignments = Roster.ReassignmentLog.Take(10).ToList();
    }
}

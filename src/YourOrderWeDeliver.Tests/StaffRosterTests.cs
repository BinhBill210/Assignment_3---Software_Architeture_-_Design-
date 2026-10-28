using YourOrderWeDeliver.Core.Common;
using YourOrderWeDeliver.Core.Coordination;
using YourOrderWeDeliver.Core.Domain;

namespace YourOrderWeDeliver.Tests;

public sealed class StaffRosterTests
{
    [Fact]
    public void RemovingAvailability_AllocatedStaff_ReassignedThroughObserver()
    {
        var world = new TestWorld();
        DeliveryStaff sam = world.AddStaff("Sam Lee", "sam@yowd.com.au");
        DeliveryStaff kim = world.AddStaff("Kim Tran", "kim@yowd.com.au");
        Delivery delivery = world.CreateAcceptedDelivery();
        delivery.AllocateStaff(sam, world.Roster, world.Now);

        sam.Availability.RemovePeriod(sam.Availability.Periods[0].Id, world.Now);

        Assert.Same(kim, delivery.AllocatedStaff);
        ReassignmentRecord record = Assert.Single(world.Roster.ReassignmentLog);
        Assert.True(record.WasReassigned);
        Assert.DoesNotContain(delivery, sam.CurrentDeliveries);
    }

    [Fact]
    public void RemovingAvailability_NoReplacement_ReturnsDeliveryToAllocationList()
    {
        var world = new TestWorld();
        DeliveryStaff sam = world.AddStaff("Sam Lee", "sam@yowd.com.au");
        Delivery delivery = world.CreateAcceptedDelivery();
        delivery.AllocateStaff(sam, world.Roster, world.Now);

        sam.Availability.RemovePeriod(sam.Availability.Periods[0].Id, world.Now);

        Assert.Null(delivery.AllocatedStaff);
        Assert.Equal(DeliveryStatus.AwaitingAllocation, delivery.Status);
        Assert.Contains(delivery.History, entry => entry.IsIssue);
        Assert.False(Assert.Single(world.Roster.ReassignmentLog).WasReassigned);
    }

    [Fact]
    public void AddingAvailability_DoesNotChangeExistingAllocations()
    {
        var world = new TestWorld();
        DeliveryStaff sam = world.AddStaff("Sam Lee", "sam@yowd.com.au", fromHour: 8, toHour: 14);
        Delivery delivery = world.CreateAcceptedDelivery();
        delivery.AllocateStaff(sam, world.Roster, world.Now);

        sam.Availability.AddPeriod(TestWorld.At(15), TestWorld.At(20), world.Now);

        Assert.Same(sam, delivery.AllocatedStaff);
        Assert.Empty(world.Roster.ReassignmentLog);
    }

    [Fact]
    public void AddPeriod_OverlappingExistingPeriod_Rejected()
    {
        var world = new TestWorld();
        DeliveryStaff sam = world.AddStaff("Sam Lee", "sam@yowd.com.au", fromHour: 8, toHour: 14);

        Assert.Throws<BusinessRuleException>(() => sam.Availability.AddPeriod(TestWorld.At(13), TestWorld.At(18), world.Now));
    }

    [Fact]
    public void DeactivateStaff_ReassignsPendingWorkAndKeepsHistory()
    {
        var world = new TestWorld();
        DeliveryStaff sam = world.AddStaff("Sam Lee", "sam@yowd.com.au");
        DeliveryStaff kim = world.AddStaff("Kim Tran", "kim@yowd.com.au");
        Delivery delivery = world.CreateAcceptedDelivery();
        delivery.AllocateStaff(sam, world.Roster, world.Now);

        IReadOnlyList<ReassignmentRecord> outcomes = world.Roster.DeactivateStaff(sam, DateOnly.FromDateTime(world.Now), world.Now);

        Assert.False(sam.IsActive);
        Assert.Same(kim, delivery.AllocatedStaff);
        Assert.Single(outcomes);
        Assert.DoesNotContain(sam, world.Roster.ActiveStaff);
        Assert.Contains(sam, world.Roster.AllStaff);
    }

    [Fact]
    public void DeactivateStaff_WhileCarryingParcels_Rejected()
    {
        var world = new TestWorld();
        DeliveryStaff sam = world.AddStaff("Sam Lee", "sam@yowd.com.au");
        Delivery delivery = world.CreateAcceptedDelivery(parcelCount: 1);
        delivery.AllocateStaff(sam, world.Roster, world.Now);
        delivery.RecordParcelCollected(delivery.Parcels[0].Id, sam, world.Now);
        delivery.RecordPickup(PaymentMethod.Cash, sam, world.Now);

        Assert.Throws<BusinessRuleException>(() => world.Roster.DeactivateStaff(sam, DateOnly.FromDateTime(world.Now), world.Now));
        Assert.True(sam.IsActive);
    }

    [Fact]
    public void RegisterNewStaff_ExistingEmail_DirectsUserToReactivate()
    {
        var world = new TestWorld();
        DeliveryStaff sam = world.AddStaff("Sam Lee", "sam@yowd.com.au");
        world.Roster.DeactivateStaff(sam, DateOnly.FromDateTime(world.Now), world.Now);

        var error = Assert.Throws<BusinessRuleException>(
            () => world.Roster.RegisterNewStaff("Sam Lee", "0498765432", "SAM@yowd.com.au", DateOnly.FromDateTime(world.Now)));

        Assert.Contains("Reactivate", error.Message, StringComparison.Ordinal);
        world.Roster.ReactivateStaff(sam, DateOnly.FromDateTime(world.Now));
        Assert.True(sam.IsActive);
    }

    [Fact]
    public void SuggestEarliestWindow_SkipsBusyTime()
    {
        var world = new TestWorld();
        world.Clock.Now = TestWorld.At(7, 30);
        DeliveryStaff sam = world.AddStaff("Sam Lee", "sam@yowd.com.au", fromHour: 9, toHour: 17);
        Delivery delivery = world.CreateAcceptedDelivery(pickupHour: 9, deliveryHour: 12);
        delivery.AllocateStaff(sam, world.Roster, world.Now);

        DeliveryWindow? suggestion = world.Roster.SuggestEarliestWindow(TestWorld.At(9), TimeSpan.FromHours(2));

        Assert.Equal(TestWorld.At(12), suggestion?.Start);
    }
}

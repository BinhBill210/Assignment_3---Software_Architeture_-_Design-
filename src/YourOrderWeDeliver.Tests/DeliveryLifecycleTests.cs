using YourOrderWeDeliver.Core.Common;
using YourOrderWeDeliver.Core.Domain;

namespace YourOrderWeDeliver.Tests;

public sealed class DeliveryLifecycleTests
{
    [Fact]
    public void AllocateStaff_SuitableStaff_LinksBothSides()
    {
        var world = new TestWorld();
        DeliveryStaff sam = world.AddStaff("Sam Lee", "sam@yowd.com.au");
        Delivery delivery = world.CreateAcceptedDelivery();

        delivery.AllocateStaff(sam, world.Roster, world.Now);

        Assert.Equal(DeliveryStatus.Allocated, delivery.Status);
        Assert.Same(sam, delivery.AllocatedStaff);
        Assert.Contains(delivery, sam.CurrentDeliveries);
    }

    [Fact]
    public void AllocateStaff_StaffWithoutAvailability_Rejected()
    {
        var world = new TestWorld();
        world.AddStaff("Sam Lee", "sam@yowd.com.au");
        DeliveryStaff lateShift = world.AddStaff("Kim Tran", "kim@yowd.com.au", fromHour: 15, toHour: 20);
        Delivery delivery = world.CreateAcceptedDelivery(pickupHour: 10, deliveryHour: 12);

        var error = Assert.Throws<BusinessRuleException>(() => delivery.AllocateStaff(lateShift, world.Roster, world.Now));

        Assert.Contains("no availability recorded", error.Message, StringComparison.Ordinal);
        Assert.Equal(DeliveryStatus.AwaitingAllocation, delivery.Status);
    }

    [Fact]
    public void AllocateStaff_OverlappingAllocation_Rejected()
    {
        var world = new TestWorld();
        DeliveryStaff sam = world.AddStaff("Sam Lee", "sam@yowd.com.au");
        world.AddStaff("Kim Tran", "kim@yowd.com.au");
        Delivery first = world.CreateAcceptedDelivery(pickupHour: 10, deliveryHour: 12);
        Delivery second = world.CreateAcceptedDelivery(pickupHour: 11, deliveryHour: 13);
        first.AllocateStaff(sam, world.Roster, world.Now);

        Assert.Throws<BusinessRuleException>(() => second.AllocateStaff(sam, world.Roster, world.Now));
        Assert.DoesNotContain(sam, world.Roster.FindSuitableStaff(second.Window, second));
    }

    [Fact]
    public void AllocateStaff_Reassignment_MovesDeliveryBetweenStaff()
    {
        var world = new TestWorld();
        DeliveryStaff sam = world.AddStaff("Sam Lee", "sam@yowd.com.au");
        DeliveryStaff kim = world.AddStaff("Kim Tran", "kim@yowd.com.au");
        Delivery delivery = world.CreateAcceptedDelivery();
        delivery.AllocateStaff(sam, world.Roster, world.Now);

        delivery.AllocateStaff(kim, world.Roster, world.Now);

        Assert.Same(kim, delivery.AllocatedStaff);
        Assert.DoesNotContain(delivery, sam.CurrentDeliveries);
        Assert.Contains(delivery, kim.CurrentDeliveries);
    }

    [Fact]
    public void RecordPickup_NotAllParcelsCollected_Rejected()
    {
        var world = new TestWorld();
        DeliveryStaff sam = world.AddStaff("Sam Lee", "sam@yowd.com.au");
        Delivery delivery = world.CreateAcceptedDelivery(parcelCount: 2);
        delivery.AllocateStaff(sam, world.Roster, world.Now);
        delivery.RecordParcelCollected(delivery.Parcels[0].Id, sam, world.Now);

        var error = Assert.Throws<BusinessRuleException>(() => delivery.RecordPickup(PaymentMethod.Card, sam, world.Now));

        Assert.Contains("only 1 of 2", error.Message, StringComparison.Ordinal);
        Assert.Null(delivery.Payment);
    }

    [Fact]
    public void FullLifecycle_PickupPaymentAndCompletion_RecordedInOrder()
    {
        var world = new TestWorld();
        DeliveryStaff sam = world.AddStaff("Sam Lee", "sam@yowd.com.au");
        Delivery delivery = world.CreateAcceptedDelivery(parcelCount: 2);
        delivery.AllocateStaff(sam, world.Roster, world.Now);

        world.Clock.Now = TestWorld.At(10, 5);
        foreach (Parcel parcel in delivery.Parcels)
        {
            delivery.RecordParcelCollected(parcel.Id, sam, world.Now);
        }

        Payment payment = delivery.RecordPickup(PaymentMethod.Cash, sam, world.Now);

        world.Clock.Now = TestWorld.At(11, 30);
        foreach (Parcel parcel in delivery.Parcels)
        {
            delivery.RecordParcelDelivered(parcel.Id, sam, world.Now);
        }

        delivery.RecordCompletion("J. Smith", sam, world.Now);

        Assert.Equal(40m, payment.Amount);
        Assert.Equal(DeliveryStatus.Delivered, delivery.Status);
        Assert.True(delivery.IsOnTime);
        Assert.Empty(sam.CurrentDeliveries);
        Assert.Contains(delivery, sam.DeliveryHistory);
    }

    [Fact]
    public void RecordParcelCollected_ByOtherStaff_Rejected()
    {
        var world = new TestWorld();
        DeliveryStaff sam = world.AddStaff("Sam Lee", "sam@yowd.com.au");
        DeliveryStaff kim = world.AddStaff("Kim Tran", "kim@yowd.com.au");
        Delivery delivery = world.CreateAcceptedDelivery();
        delivery.AllocateStaff(sam, world.Roster, world.Now);

        Assert.Throws<BusinessRuleException>(() => delivery.RecordParcelCollected(delivery.Parcels[0].Id, kim, world.Now));
    }

    [Fact]
    public void UndoParcelCollected_BeforePickup_ClearsMark()
    {
        var world = new TestWorld();
        DeliveryStaff sam = world.AddStaff("Sam Lee", "sam@yowd.com.au");
        Delivery delivery = world.CreateAcceptedDelivery();
        delivery.AllocateStaff(sam, world.Roster, world.Now);
        string parcelId = delivery.Parcels[0].Id;
        delivery.RecordParcelCollected(parcelId, sam, world.Now);

        delivery.UndoParcelCollected(parcelId, sam, world.Now);

        Assert.False(delivery.Parcels[0].IsCollected);
    }

    [Fact]
    public void RecordCompletion_BlankRecipient_Rejected()
    {
        var world = new TestWorld();
        DeliveryStaff sam = world.AddStaff("Sam Lee", "sam@yowd.com.au");
        Delivery delivery = world.CreateAcceptedDelivery(parcelCount: 1);
        delivery.AllocateStaff(sam, world.Roster, world.Now);
        delivery.RecordParcelCollected(delivery.Parcels[0].Id, sam, world.Now);
        delivery.RecordPickup(PaymentMethod.Card, sam, world.Now);
        delivery.RecordParcelDelivered(delivery.Parcels[0].Id, sam, world.Now);

        Assert.Throws<BusinessRuleException>(() => delivery.RecordCompletion("  ", sam, world.Now));
        Assert.Equal(DeliveryStatus.PickedUp, delivery.Status);
    }
}

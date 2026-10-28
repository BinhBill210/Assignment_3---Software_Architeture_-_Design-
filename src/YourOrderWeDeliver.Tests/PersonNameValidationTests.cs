using YourOrderWeDeliver.Core.Common;
using YourOrderWeDeliver.Core.Domain;

namespace YourOrderWeDeliver.Tests;

public sealed class PersonNameValidationTests
{
    [Theory]
    [InlineData("Vihanga Peiris")]
    [InlineData("Anne-Marie")]
    [InlineData("O'Connor")]
    [InlineData("José Silva")]
    [InlineData("J. Smith")]
    public void ValidName_AcceptedForCustomerStaffAndRecipient(string name)
    {
        var world = new TestWorld();
        DeliveryStaff staff = world.AddStaff(name, "staff@yowd.com.au");
        RequestDetails details = TestWorld.ValidDetails() with
        {
            CustomerName = name,
        };

        DeliveryRequest request = world.Register.RecordRequest(
            details, RequestChannel.CustomerOnline, world.Now);

        Assert.Equal(name, request.Customer.Name);
        Assert.Equal(name, staff.Name);

        Delivery delivery = PrepareForCompletion(world, request, staff);
        delivery.RecordCompletion(name, staff, world.Now);

        Assert.Equal(name, delivery.ReceivedBy);
        Assert.Equal(DeliveryStatus.Delivered, delivery.Status);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("John123")]
    [InlineData("---")]
    [InlineData("John@Smith")]
    [InlineData("   ")]
    [InlineData(null)]
    public void InvalidName_RejectedWithoutChangingRecords(string? name)
    {
        var world = new TestWorld();
        RequestDetails details = TestWorld.ValidDetails() with
        {
            CustomerName = name,
        };

        Assert.Throws<BusinessRuleException>(
            () => world.Register.RecordRequest(
                details, RequestChannel.CustomerOnline, world.Now));

        Assert.Empty(world.Register.Requests);
        Assert.Empty(world.Register.Customers);

        Assert.Throws<BusinessRuleException>(
            () => world.Roster.RegisterNewStaff(
                name!, "0498765432", "invalid@yowd.com.au",
                DateOnly.FromDateTime(world.Now)));

        Assert.Empty(world.Roster.AllStaff);

        DeliveryStaff staff = world.AddStaff("Sam Lee", "sam@yowd.com.au");

        Assert.Throws<BusinessRuleException>(
            () => world.Roster.UpdateStaffDetails(
                staff, name!, staff.Phone, staff.Email));

        Assert.Equal("Sam Lee", staff.Name);

        DeliveryRequest request = world.RecordRequest();
        Delivery delivery = PrepareForCompletion(world, request, staff);
        int historyCount = delivery.History.Count;

        Assert.Throws<BusinessRuleException>(
            () => delivery.RecordCompletion(name, staff, world.Now));

        Assert.Equal(DeliveryStatus.PickedUp, delivery.Status);
        Assert.Null(delivery.ReceivedBy);
        Assert.Null(delivery.DeliveredAt);
        Assert.Equal(historyCount, delivery.History.Count);
    }

    private static Delivery PrepareForCompletion(
        TestWorld world, DeliveryRequest request, DeliveryStaff staff)
    {
        request.PrepareQuote(TestWorld.Terms(), world.Roster, world.Now);
        Delivery delivery = world.Register.RecordQuoteResponse(
            request, accepted: true, world.Now)!;
        delivery.AllocateStaff(staff, world.Roster, world.Now);

        world.Clock.Now = TestWorld.At(10);

        foreach (Parcel parcel in delivery.Parcels)
        {
            delivery.RecordParcelCollected(parcel.Id, staff, world.Now);
        }

        delivery.RecordPickup(PaymentMethod.Card, staff, world.Now);
        world.Clock.Now = TestWorld.At(11);

        foreach (Parcel parcel in delivery.Parcels)
        {
            delivery.RecordParcelDelivered(parcel.Id, staff, world.Now);
        }

        return delivery;
    }
}
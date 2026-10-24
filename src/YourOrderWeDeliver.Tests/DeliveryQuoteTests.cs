using YourOrderWeDeliver.Core.Common;
using YourOrderWeDeliver.Core.Domain;
using YourOrderWeDeliver.Core.Pricing;

namespace YourOrderWeDeliver.Tests;

public sealed class DeliveryQuoteTests
{
    [Fact]
    public void PrepareQuote_NoStaffAvailable_RejectedWithSuggestedTime()
    {
        var world = new TestWorld();
        world.AddStaff("Sam Lee", "sam@yowd.com.au", fromHour: 13, toHour: 18);
        DeliveryRequest request = world.RecordRequest();

        var error = Assert.Throws<BusinessRuleException>(
            () => request.PrepareQuote(TestWorld.Terms(pickupHour: 10, deliveryHour: 12), world.Roster, world.Now));

        Assert.Contains("No active delivery staff are available", error.Message, StringComparison.Ordinal);
        Assert.Contains("1:00 pm", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(request.Quote);
    }

    [Theory]
    [InlineData(0, 10, 12)]
    [InlineData(40.555, 10, 12)]
    [InlineData(40, 12, 10)]
    [InlineData(40, 7, 9)]
    public void PrepareQuote_InvalidTerms_Rejected(double cost, int pickupHour, int deliveryHour)
    {
        var world = new TestWorld();
        world.AddStaff("Sam Lee", "sam@yowd.com.au");
        DeliveryRequest request = world.RecordRequest();

        Assert.Throws<BusinessRuleException>(() => request.PrepareQuote(
            new QuoteTerms((decimal)cost, TestWorld.At(pickupHour), TestWorld.At(deliveryHour)), world.Roster, world.Now));
    }

    [Fact]
    public void ValidateQuote_ValidTerms_RecordsNothing()
    {
        var world = new TestWorld();
        world.AddStaff("Sam Lee", "sam@yowd.com.au");
        DeliveryRequest request = world.RecordRequest();

        request.ValidateQuote(TestWorld.Terms(), world.Roster, world.Now);

        Assert.Null(request.Quote);
        Assert.Equal(RequestStatus.Submitted, request.Status);
    }

    [Fact]
    public void PrepareQuote_BeforeResponse_RevisesExistingQuote()
    {
        var world = new TestWorld();
        world.AddStaff("Sam Lee", "sam@yowd.com.au");
        DeliveryRequest request = world.RecordRequest();

        DeliveryQuote first = request.PrepareQuote(TestWorld.Terms(cost: 40m), world.Roster, world.Now);
        DeliveryQuote revised = request.PrepareQuote(TestWorld.Terms(pickupHour: 14, deliveryHour: 16, cost: 35m), world.Roster, world.Now);

        Assert.Same(first, revised);
        Assert.Equal(2, revised.Revision);
        Assert.Equal(35m, revised.Cost);
        Assert.Equal(RequestStatus.Quoted, request.Status);
    }

    [Fact]
    public void RecordQuoteResponse_Accepted_CreatesAndRegistersDelivery()
    {
        var world = new TestWorld();
        world.AddStaff("Sam Lee", "sam@yowd.com.au");

        Delivery delivery = world.CreateAcceptedDelivery();

        Assert.Equal(RequestStatus.Accepted, delivery.Request.Status);
        Assert.Equal(DeliveryStatus.AwaitingAllocation, delivery.Status);
        Assert.Same(delivery, delivery.Request.Delivery);
        Assert.All(delivery.Parcels, parcel => Assert.Same(delivery, parcel.Delivery));
        Assert.Contains(delivery, world.Register.DeliveriesAwaitingAllocation);
    }

    [Fact]
    public void RecordQuoteResponse_Declined_KeepsRequestWithoutDelivery()
    {
        var world = new TestWorld();
        world.AddStaff("Sam Lee", "sam@yowd.com.au");
        DeliveryRequest request = world.RecordRequest();
        request.PrepareQuote(TestWorld.Terms(), world.Roster, world.Now);

        Delivery? delivery = world.Register.RecordQuoteResponse(request, accepted: false, world.Now);

        Assert.Null(delivery);
        Assert.Equal(RequestStatus.Declined, request.Status);
        Assert.Empty(world.Register.Deliveries);
        Assert.Throws<BusinessRuleException>(() => request.PrepareQuote(TestWorld.Terms(), world.Roster, world.Now));
    }

    [Fact]
    public void RecordQuoteResponse_Twice_Rejected()
    {
        var world = new TestWorld();
        world.AddStaff("Sam Lee", "sam@yowd.com.au");
        Delivery delivery = world.CreateAcceptedDelivery();

        Assert.Throws<BusinessRuleException>(() => world.Register.RecordQuoteResponse(delivery.Request, accepted: false, world.Now));
        Assert.Single(world.Register.Deliveries);
    }

    [Fact]
    public void RecordQuoteResponse_WithoutQuote_Rejected()
    {
        var world = new TestWorld();
        DeliveryRequest request = world.RecordRequest();

        Assert.Throws<BusinessRuleException>(() => world.Register.RecordQuoteResponse(request, accepted: true, world.Now));
    }

    [Fact]
    public void RecordQuoteResponse_AfterValidityPeriod_Rejected()
    {
        var world = new TestWorld();
        world.AddStaff("Sam Lee", "sam@yowd.com.au");
        DeliveryRequest request = world.RecordRequest();
        request.PrepareQuote(TestWorld.Terms(), world.Roster, world.Now);
        world.Clock.Now = world.Now + DeliveryQuote.ValidityPeriod + TimeSpan.FromMinutes(1);

        var error = Assert.Throws<BusinessRuleException>(
            () => world.Register.RecordQuoteResponse(request, accepted: true, world.Now));

        Assert.Contains("expired", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StandardPricingPolicy_CombinesParcelWeightAndFragileFees()
    {
        var world = new TestWorld();
        DeliveryRequest request = world.RecordRequest(parcelCount: 2);

        decimal cost = request.SuggestCost(StandardPricingPolicy.Default());

        Assert.Equal(15m + 5m + (5m * 1.5m) + 4m, cost);
    }
}

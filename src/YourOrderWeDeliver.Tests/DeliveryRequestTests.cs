using YourOrderWeDeliver.Core.Common;
using YourOrderWeDeliver.Core.Domain;

namespace YourOrderWeDeliver.Tests;

public sealed class DeliveryRequestTests
{
    [Fact]
    public void RecordRequest_ValidDetails_AssignsReferenceAndParcels()
    {
        var world = new TestWorld();

        DeliveryRequest request = world.RecordRequest();

        Assert.Equal("REQ-100001", request.Reference);
        Assert.Equal(RequestStatus.Submitted, request.Status);
        Assert.Equal(2, request.Parcels.Count);
        Assert.All(request.Parcels, parcel => Assert.Same(request, parcel.Request));
        Assert.Equal("0412345678", request.Customer.Phone);
        Assert.Same(request, world.Register.FindRequest("req-100001"));
    }

    [Fact]
    public void RecordRequest_ReturningCustomer_LinksExistingCustomer()
    {
        var world = new TestWorld();

        DeliveryRequest first = world.RecordRequest();
        DeliveryRequest second = world.RecordRequest(email: "AVA.NGUYEN@example.com");

        Assert.Same(first.Customer, second.Customer);
        Assert.Equal(2, first.Customer.Requests.Count);
        Assert.Single(world.Register.Customers);
    }

    [Theory]
    [InlineData("", "ava@example.com", "0412345678", "3122", "Customer name is required")]
    [InlineData("Ava", "not-an-email", "0412345678", "3122", "not a valid email")]
    [InlineData("Ava", "ava@example.com", "12345", "3122", "Australian number")]
    [InlineData("Ava", "ava@example.com", "0412345678", "31A2", "exactly four digits")]
    [InlineData("Ava", "ava@example.com", "0412345678", "2000", "outside our Greater Melbourne service area")]
    public void RecordRequest_InvalidInput_RejectedWithoutRecordingAnything(
        string name, string email, string phone, string pickupPostcode, string expectedMessage)
    {
        var world = new TestWorld();
        RequestDetails details = TestWorld.ValidDetails(pickupPostcode: pickupPostcode) with
        {
            CustomerName = name,
            CustomerEmail = email,
            CustomerPhone = phone,
        };

        var error = Assert.Throws<BusinessRuleException>(
            () => world.Register.RecordRequest(details, RequestChannel.OfficeStaff, world.Now));

        Assert.Contains(expectedMessage, error.Message, StringComparison.Ordinal);
        Assert.Empty(world.Register.Requests);
        Assert.Empty(world.Register.Customers);
    }
    [Theory]
    [InlineData("Different Person", "0412 345 678")]
    [InlineData("Ava Nguyen", "0499 888 777")]
    [InlineData("Different Person", "0499 888 777")]
    public void ReturningCustomer_ConflictingDetails_RejectedWithoutChangingExistingRecords(
        string name, string phone)
    {
        var world = new TestWorld();
        DeliveryRequest first = world.RecordRequest();

        RequestDetails conflictingDetails = TestWorld.ValidDetails() with
        {
            CustomerName = name,
            CustomerPhone = phone,
        };

        var validationError = Assert.Throws<BusinessRuleException>(
            () => world.Register.ValidateRequest(conflictingDetails, world.Now));

        var submissionError = Assert.Throws<BusinessRuleException>(
            () => world.Register.RecordRequest(
                conflictingDetails, RequestChannel.CustomerOnline, world.Now));

        Assert.Contains("do not match", validationError.Message, StringComparison.Ordinal);
        Assert.Contains("do not match", submissionError.Message, StringComparison.Ordinal);
        Assert.Equal("Ava Nguyen", first.Customer.Name);
        Assert.Equal("0412345678", first.Customer.Phone);
        Assert.Single(world.Register.Customers);
        Assert.Single(world.Register.Requests);
        Assert.Single(first.Customer.Requests);

        Assert.Same(
            first,
            world.Register.FindRequestForCustomer(first.Reference, "0412345678"));

        if (phone == "0499 888 777")
        {
            Assert.Null(
                world.Register.FindRequestForCustomer(first.Reference, phone));
        }

        DeliveryRequest next = world.RecordRequest();
        Assert.Equal("REQ-100002", next.Reference);
    }
    [Fact]
    public void RecordRequest_NoParcels_Rejected()
    {
        var world = new TestWorld();
        RequestDetails details = TestWorld.ValidDetails() with { Parcels = [] };

        Assert.Throws<BusinessRuleException>(() => world.Register.RecordRequest(details, RequestChannel.CustomerOnline, world.Now));
    }

    [Fact]
    public void RecordRequest_OverweightParcel_Rejected()
    {
        var world = new TestWorld();
        RequestDetails details = TestWorld.ValidDetails() with { Parcels = [new ParcelDetails("Piano", 250m, false)] };

        var error = Assert.Throws<BusinessRuleException>(
            () => world.Register.RecordRequest(details, RequestChannel.CustomerOnline, world.Now));

        Assert.Contains("Parcel 1 weight", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RecordRequest_PreferredDateInPast_Rejected()
    {
        var world = new TestWorld();
        RequestDetails details = TestWorld.ValidDetails() with
        {
            PreferredPickupDate = DateOnly.FromDateTime(TestWorld.Start).AddDays(-1),
        };

        Assert.Throws<BusinessRuleException>(() => world.Register.RecordRequest(details, RequestChannel.CustomerOnline, world.Now));
    }

    [Fact]
    public void FindRequestForCustomer_RequiresMatchingContact()
    {
        var world = new TestWorld();
        DeliveryRequest request = world.RecordRequest();

        Assert.Null(world.Register.FindRequestForCustomer(request.Reference, "someone.else@example.com"));
        Assert.Same(request, world.Register.FindRequestForCustomer(request.Reference, "0412 345 678"));
        Assert.Same(request, world.Register.FindRequestForCustomer(request.Reference, "Ava.Nguyen@Example.com"));
    }

    [Fact]
    public void UpdateDetails_AfterQuotePrepared_Rejected()
    {
        var world = new TestWorld();
        world.AddStaff("Sam Lee", "sam@yowd.com.au");
        DeliveryRequest request = world.RecordRequest();
        request.PrepareQuote(TestWorld.Terms(), world.Roster, world.Now);

        Assert.Throws<BusinessRuleException>(() => request.UpdateDetails(
            request.PickupAddress,
            request.DestinationAddress,
            request.PreferredPickupDate,
            null,
            world.Register.ServiceArea,
            world.Now));
    }
}

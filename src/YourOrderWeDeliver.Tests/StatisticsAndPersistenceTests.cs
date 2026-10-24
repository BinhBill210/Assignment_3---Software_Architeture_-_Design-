using YourOrderWeDeliver.Core.Coordination;
using YourOrderWeDeliver.Core.Domain;
using YourOrderWeDeliver.Core.Persistence;
using YourOrderWeDeliver.Core.Pricing;

namespace YourOrderWeDeliver.Tests;

public sealed class DeliveryStatisticsTests
{
    [Theory]
    [InlineData(ReportingPeriod.Day, "2026-11-04", "2026-11-05")]
    [InlineData(ReportingPeriod.Week, "2026-11-02", "2026-11-09")]
    [InlineData(ReportingPeriod.Month, "2026-11-01", "2026-12-01")]
    [InlineData(ReportingPeriod.Year, "2026-01-01", "2027-01-01")]
    [InlineData(ReportingPeriod.YearToDate, "2026-01-01", "2026-11-05")]
    public void ResolveRange_Wednesday4November(ReportingPeriod period, string expectedFrom, string expectedTo)
    {
        (DateTime from, DateTime to) = DeliveryStatistics.ResolveRange(period, new DateOnly(2026, 11, 4));

        Assert.Equal(DateTime.Parse(expectedFrom, System.Globalization.CultureInfo.InvariantCulture), from);
        Assert.Equal(DateTime.Parse(expectedTo, System.Globalization.CultureInfo.InvariantCulture), to);
    }

    [Fact]
    public void Calculate_CountsCompletedDeliveriesOverallAndPerStaff()
    {
        var world = new TestWorld();
        DeliveryStaff sam = world.AddStaff("Sam Lee", "sam@yowd.com.au");
        Delivery delivery = world.CreateAcceptedDelivery(parcelCount: 2);
        world.RecordRequest(email: "declined@example.com");
        Complete(world, delivery, sam);

        DeliveryStatistics overall = DeliveryStatistics.Calculate(
            world.Register, ReportingPeriod.Day, DateOnly.FromDateTime(TestWorld.Start));
        DeliveryStatistics forSam = DeliveryStatistics.Calculate(
            world.Register, ReportingPeriod.Week, DateOnly.FromDateTime(TestWorld.Start), sam);

        Assert.Equal(2, overall.RequestsReceived);
        Assert.Equal(1, overall.QuotesAccepted);
        Assert.Equal(1, overall.DeliveriesCompleted);
        Assert.Equal(2, overall.ParcelsDelivered);
        Assert.Equal(40m, overall.PaymentsRecorded);
        Assert.Equal(1.0, overall.OnTimeRate);
        Assert.Equal(sam.Id, Assert.Single(overall.StaffBreakdown).StaffId);
        Assert.Equal(1, forSam.DeliveriesCompleted);
    }

    internal static void Complete(TestWorld world, Delivery delivery, DeliveryStaff staff)
    {
        delivery.AllocateStaff(staff, world.Roster, world.Now);
        foreach (Parcel parcel in delivery.Parcels)
        {
            delivery.RecordParcelCollected(parcel.Id, staff, world.Now);
        }

        delivery.RecordPickup(PaymentMethod.Card, staff, world.Now);
        foreach (Parcel parcel in delivery.Parcels)
        {
            delivery.RecordParcelDelivered(parcel.Id, staff, world.Now);
        }

        delivery.RecordCompletion("Reception", staff, world.Now);
    }
}

[Collection(SingletonTestGroup.Name)]
public sealed class PersistenceTests : IDisposable
{
    private readonly string _filePath = Path.Combine(Path.GetTempPath(), $"yowd-test-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        DeliveryManagementSystem.ResetForTesting();
        File.Delete(_filePath);
    }

    [Fact]
    public void SaveAndRestart_RestoresLinkedObjectModel()
    {
        var world = new TestWorld();
        DeliveryStaff sam = world.AddStaff("Sam Lee", "sam@yowd.com.au");
        Delivery completed = world.CreateAcceptedDelivery();
        DeliveryStatisticsTests.Complete(world, completed, sam);
        Delivery pending = world.CreateAcceptedDelivery(pickupHour: 14, deliveryHour: 16);
        pending.AllocateStaff(sam, world.Roster, world.Now);
        var store = new JsonFileDataStore(_filePath);
        store.Save(SnapshotMapper.CreateSnapshot(world.Register, world.Roster));

        DeliveryManagementSystem system = DeliveryManagementSystem.Initialise(
            store, ServiceArea.GreaterMelbourne(), StandardPricingPolicy.Default(), world.Clock);

        Assert.True(system.IsReady);
        DeliveryStaff restoredSam = system.Roster.Get(sam.Id);
        Delivery restoredPending = system.Register.FindDelivery(pending.Id)!;
        Assert.Same(restoredSam, restoredPending.AllocatedStaff);
        Assert.Contains(restoredPending, restoredSam.CurrentDeliveries);
        Assert.Equal(2, restoredSam.DeliveryHistory.Count());
        Assert.Equal(DeliveryStatus.Delivered, system.Register.FindDelivery(completed.Id)!.Status);
        Assert.Equal(40m, system.Register.FindDelivery(completed.Id)!.Payment!.Amount);
        Assert.Equal(completed.History.Count, system.Register.FindDelivery(completed.Id)!.History.Count);

        restoredSam.Availability.RemovePeriod(restoredSam.Availability.Periods[0].Id, world.Now);
        Assert.Equal(DeliveryStatus.AwaitingAllocation, restoredPending.Status);
        Assert.Equal("REQ-100003", system.Register.RecordRequest(
            TestWorld.ValidDetails(), RequestChannel.CustomerOnline, world.Now).Reference);
    }

    [Fact]
    public void Initialise_SecondTime_Rejected()
    {
        var store = new InMemoryDataStore();
        DeliveryManagementSystem.Initialise(store, ServiceArea.GreaterMelbourne(), StandardPricingPolicy.Default(), new FakeClock());

        Assert.Throws<InvalidOperationException>(() => DeliveryManagementSystem.Initialise(
            store, ServiceArea.GreaterMelbourne(), StandardPricingPolicy.Default(), new FakeClock()));
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SingletonTestGroup
{
    public const string Name = "Singleton";
}

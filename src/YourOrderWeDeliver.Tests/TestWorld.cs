using YourOrderWeDeliver.Core.Common;
using YourOrderWeDeliver.Core.Coordination;
using YourOrderWeDeliver.Core.Domain;

namespace YourOrderWeDeliver.Tests;

internal sealed class FakeClock : IClock
{
    public DateTime Now { get; set; }
}

/// <summary>Builds a small, fully linked object model for one test.</summary>
internal sealed class TestWorld
{
    /// <summary>Monday 2 November 2026, 8:00 am.</summary>
    public static readonly DateTime Start = new(2026, 11, 2, 8, 0, 0, DateTimeKind.Unspecified);

    public FakeClock Clock { get; } = new() { Now = Start };

    public StaffRoster Roster { get; } = new();

    public DeliveryRegister Register { get; } = new(ServiceArea.GreaterMelbourne());

    public DateTime Now => Clock.Now;

    public static DateTime At(int hour, int minute = 0) => Start.Date.AddHours(hour).AddMinutes(minute);

    public static RequestDetails ValidDetails(
        string email = "ava.nguyen@example.com", int parcelCount = 2, string pickupPostcode = "3122")
    {
        List<ParcelDetails> parcels = Enumerable.Range(1, parcelCount)
            .Select(number => new ParcelDetails($"Box {number}", 2.5m, number == 1))
            .ToList();
        return new RequestDetails(
            "Ava Nguyen",
            email,
            "0412 345 678",
            new AddressDetails("12 Glenferrie Road", "Hawthorn", pickupPostcode),
            new AddressDetails("80 Collins Street", "Melbourne", "3000"),
            DateOnly.FromDateTime(Start),
            "Leave at reception",
            parcels);
    }

    public static QuoteTerms Terms(int pickupHour = 10, int deliveryHour = 12, decimal cost = 40m)
    {
        return new QuoteTerms(cost, At(pickupHour), At(deliveryHour));
    }

    public DeliveryStaff AddStaff(string name, string email, int fromHour = 8, int toHour = 20)
    {
        DeliveryStaff staff = Roster.RegisterNewStaff(name, "0498 765 432", email, DateOnly.FromDateTime(Start));
        staff.Availability.AddPeriod(At(fromHour), At(toHour), Now);
        return staff;
    }

    public DeliveryRequest RecordRequest(string email = "ava.nguyen@example.com", int parcelCount = 2)
    {
        return Register.RecordRequest(ValidDetails(email, parcelCount), RequestChannel.CustomerOnline, Now);
    }

    public Delivery CreateAcceptedDelivery(int pickupHour = 10, int deliveryHour = 12, int parcelCount = 2)
    {
        DeliveryRequest request = RecordRequest(parcelCount: parcelCount);
        request.PrepareQuote(Terms(pickupHour, deliveryHour), Roster, Now);
        return Register.RecordQuoteResponse(request, accepted: true, Now)!;
    }
}

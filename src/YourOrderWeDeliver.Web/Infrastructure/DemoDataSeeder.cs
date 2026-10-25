using YourOrderWeDeliver.Core.Coordination;
using YourOrderWeDeliver.Core.Domain;

namespace YourOrderWeDeliver.Web.Infrastructure;

/// <summary>
/// Creates sample delivery staff with two weeks of availability the first time the system runs,
/// so that quoting and allocation can be demonstrated immediately. Requests are not seeded, so every
/// scenario starts from an empty screen.
/// </summary>
public static class DemoDataSeeder
{
    private static readonly (string Name, string Phone, string Email, int FromHour, int ToHour)[] DemoStaff =
    [
        ("Sam Lee", "0412 111 222", "sam.lee@yowd.com.au", 8, 17),
        ("Kim Tran", "0413 333 444", "kim.tran@yowd.com.au", 10, 19),
        ("Priya Shah", "0414 555 666", "priya.shah@yowd.com.au", 7, 15),
        ("Tom Walker", "0415 777 888", "tom.walker@yowd.com.au", 12, 20),
    ];

    /// <summary>Seeds demo staff when the roster is empty and saves the result.</summary>
    public static void SeedIfEmpty(DeliveryManagementSystem system)
    {
        ArgumentNullException.ThrowIfNull(system);
        if (system.Roster.AllStaff.Any())
        {
            return;
        }

        DateTime now = system.Clock.Now;
        DateOnly today = DateOnly.FromDateTime(now);
        foreach ((string name, string phone, string email, int fromHour, int toHour) in DemoStaff)
        {
            DeliveryStaff staff = system.Roster.RegisterNewStaff(name, phone, email, today.AddMonths(-6));
            for (int day = 0; day < 14; day++)
            {
                DateTime date = now.Date.AddDays(day);
                DateTime end = date.AddHours(toHour);
                if (end > now)
                {
                    staff.Availability.AddPeriod(date.AddHours(fromHour), end, now);
                }
            }
        }

        system.SaveChanges();
    }
}

using YourOrderWeDeliver.Core.Coordination;

namespace YourOrderWeDeliver.Core.Domain;

/// <summary>Data-holder: completed work by one staff member within a reporting period.</summary>
/// <param name="StaffId">Staff identifier.</param>
/// <param name="StaffName">Staff name.</param>
/// <param name="DeliveriesCompleted">Deliveries completed.</param>
/// <param name="ParcelsDelivered">Parcels delivered.</param>
/// <param name="OnTimeDeliveries">Deliveries completed by the end of their scheduled window.</param>
public sealed record StaffActivitySummary(
    string StaffId, string StaffName, int DeliveriesCompleted, int ParcelsDelivered, int OnTimeDeliveries);

/// <summary>
/// Calculated summary of delivery activity for a reporting period, for the whole business or one
/// staff member (A2 CRC 5.9). Created on demand and discarded after display (A2 Table 8).
/// </summary>
public sealed class DeliveryStatistics
{
    private DeliveryStatistics(ReportingPeriod period, DateTime from, DateTime to, DeliveryStaff? staff)
    {
        Period = period;
        From = from;
        To = to;
        Staff = staff;
    }

    /// <summary>Gets the reporting period type.</summary>
    public ReportingPeriod Period { get; }

    /// <summary>Gets the inclusive start of the period.</summary>
    public DateTime From { get; }

    /// <summary>Gets the exclusive end of the period.</summary>
    public DateTime To { get; }

    /// <summary>Gets the staff member reported on, or <c>null</c> for overall statistics.</summary>
    public DeliveryStaff? Staff { get; }

    /// <summary>Gets the number of requests recorded (overall statistics only).</summary>
    public int RequestsReceived { get; private set; }

    /// <summary>Gets the number of quotes accepted (overall statistics only).</summary>
    public int QuotesAccepted { get; private set; }

    /// <summary>Gets the number of quotes declined (overall statistics only).</summary>
    public int QuotesDeclined { get; private set; }

    /// <summary>Gets the number of deliveries completed.</summary>
    public int DeliveriesCompleted { get; private set; }

    /// <summary>Gets the number of parcels delivered.</summary>
    public int ParcelsDelivered { get; private set; }

    /// <summary>Gets the number of deliveries completed on time.</summary>
    public int OnTimeDeliveries { get; private set; }

    /// <summary>Gets the payments recorded at pickup during the period.</summary>
    public decimal PaymentsRecorded { get; private set; }

    /// <summary>Gets the average time from pickup to completion, if any delivery was completed.</summary>
    public TimeSpan? AveragePickupToDelivery { get; private set; }

    /// <summary>Gets the proportion of deliveries completed on time (0 to 1), if any were completed.</summary>
    public double? OnTimeRate => DeliveriesCompleted == 0 ? null : (double)OnTimeDeliveries / DeliveriesCompleted;

    /// <summary>Gets per-staff activity (overall statistics only), busiest first.</summary>
    public IReadOnlyList<StaffActivitySummary> StaffBreakdown { get; private set; } = [];

    /// <summary>Calculates statistics from the register (R1 to R4).</summary>
    /// <param name="register">Source of delivery records.</param>
    /// <param name="period">Reporting period type.</param>
    /// <param name="referenceDate">Any day inside the period; the last day for year-to-date.</param>
    /// <param name="staff">Staff member to report on, or <c>null</c> for the whole business.</param>
    public static DeliveryStatistics Calculate(
        DeliveryRegister register, ReportingPeriod period, DateOnly referenceDate, DeliveryStaff? staff = null)
    {
        ArgumentNullException.ThrowIfNull(register);
        (DateTime from, DateTime to) = ResolveRange(period, referenceDate);
        var statistics = new DeliveryStatistics(period, from, to, staff);

        List<Delivery> completed = register.CompletedDeliveriesBetween(from, to)
            .Where(delivery => staff is null || ReferenceEquals(delivery.AllocatedStaff, staff))
            .ToList();
        statistics.DeliveriesCompleted = completed.Count;
        statistics.ParcelsDelivered = completed.Sum(delivery => delivery.Parcels.Count);
        statistics.OnTimeDeliveries = completed.Count(delivery => delivery.IsOnTime);
        if (completed.Count > 0)
        {
            double averageTicks = completed.Average(delivery => (delivery.DeliveredAt!.Value - delivery.PickedUpAt!.Value).Ticks);
            statistics.AveragePickupToDelivery = TimeSpan.FromTicks((long)averageTicks);
        }

        statistics.PaymentsRecorded = register.Deliveries
            .Where(delivery => staff is null || ReferenceEquals(delivery.AllocatedStaff, staff))
            .Select(delivery => delivery.Payment)
            .Where(payment => payment is not null && payment.ReceivedAt >= from && payment.ReceivedAt < to)
            .Sum(payment => payment!.Amount);

        if (staff is null)
        {
            statistics.RequestsReceived = register.RequestsSubmittedBetween(from, to).Count();
            List<DeliveryQuote> responded = register.Requests
                .Select(request => request.Quote)
                .OfType<DeliveryQuote>()
                .Where(quote => quote.RespondedAt >= from && quote.RespondedAt < to)
                .ToList();
            statistics.QuotesAccepted = responded.Count(quote => quote.Response == QuoteResponse.Accepted);
            statistics.QuotesDeclined = responded.Count(quote => quote.Response == QuoteResponse.Declined);
            statistics.StaffBreakdown = completed
                .GroupBy(delivery => delivery.AllocatedStaff!)
                .Select(group => new StaffActivitySummary(
                    group.Key.Id,
                    group.Key.Name,
                    group.Count(),
                    group.Sum(delivery => delivery.Parcels.Count),
                    group.Count(delivery => delivery.IsOnTime)))
                .OrderByDescending(summary => summary.DeliveriesCompleted)
                .ThenBy(summary => summary.StaffName, StringComparer.CurrentCulture)
                .ToList();
        }

        return statistics;
    }

    /// <summary>Converts a period type and reference date into a [from, to) range.</summary>
    public static (DateTime From, DateTime To) ResolveRange(ReportingPeriod period, DateOnly referenceDate)
    {
        DateTime day = referenceDate.ToDateTime(TimeOnly.MinValue);
        switch (period)
        {
            case ReportingPeriod.Day:
                return (day, day.AddDays(1));
            case ReportingPeriod.Week:
                int daysSinceMonday = ((int)day.DayOfWeek + 6) % 7;
                DateTime monday = day.AddDays(-daysSinceMonday);
                return (monday, monday.AddDays(7));
            case ReportingPeriod.Month:
                var firstOfMonth = new DateTime(day.Year, day.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
                return (firstOfMonth, firstOfMonth.AddMonths(1));
            case ReportingPeriod.Year:
                var firstOfYear = new DateTime(day.Year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
                return (firstOfYear, firstOfYear.AddYears(1));
            case ReportingPeriod.YearToDate:
                return (new DateTime(day.Year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), day.AddDays(1));
            default:
                throw new ArgumentOutOfRangeException(nameof(period), period, "Unsupported reporting period.");
        }
    }
}

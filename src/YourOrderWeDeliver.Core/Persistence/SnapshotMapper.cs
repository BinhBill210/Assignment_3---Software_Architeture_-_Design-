using YourOrderWeDeliver.Core.Coordination;
using YourOrderWeDeliver.Core.Domain;

namespace YourOrderWeDeliver.Core.Persistence;

/// <summary>
/// Converts between the object model and <see cref="SystemSnapshot"/>. Restoring follows the
/// start-up order in A2 Section 7.2: staff before deliveries, so deliveries can relink their staff.
/// </summary>
internal static class SnapshotMapper
{
    public static SystemSnapshot CreateSnapshot(DeliveryRegister register, StaffRoster roster)
    {
        return new SystemSnapshot
        {
            Counters = new CounterRecord(
                register.LastCustomerNumber, register.LastRequestNumber, register.LastDeliveryNumber, roster.LastStaffNumber),
            Staff = roster.AllStaff.Select(ToRecord).ToList(),
            Customers = register.Customers
                .Select(customer => new CustomerRecord(customer.Id, customer.Name, customer.Email, customer.Phone))
                .ToList(),
            Requests = register.Requests.OrderBy(request => request.SubmittedAt).Select(ToRecord).ToList(),
            Deliveries = register.Deliveries.OrderBy(delivery => delivery.CreatedAt).Select(ToRecord).ToList(),
        };
    }

    /// <summary>Start-up steps 2 to 5: roster, staff, their availability, observer registration.</summary>
    public static StaffRoster RestoreRoster(SystemSnapshot snapshot)
    {
        var roster = new StaffRoster { LastStaffNumber = snapshot.Counters.Staff };
        foreach (StaffRecord record in snapshot.Staff)
        {
            var staff = new DeliveryStaff(record.Id, record.Name, record.Phone, record.Email, record.JoinedOn)
            {
                IsActive = record.IsActive,
                LeftOn = record.LeftOn,
            };
            foreach (AvailabilityPeriodRecord period in record.Availability)
            {
                staff.Availability.RestorePeriod(new AvailabilityPeriod(period.Id, new DeliveryWindow(period.Start, period.End)));
            }

            roster.Enrol(staff);
        }

        return roster;
    }

    /// <summary>Start-up steps 6 to 10: register, customers, requests with parcels and quotes, deliveries.</summary>
    public static DeliveryRegister RestoreRegister(SystemSnapshot snapshot, ServiceArea serviceArea, StaffRoster roster)
    {
        var register = new DeliveryRegister(serviceArea)
        {
            LastCustomerNumber = snapshot.Counters.Customer,
            LastRequestNumber = snapshot.Counters.Request,
            LastDeliveryNumber = snapshot.Counters.Delivery,
        };

        var customers = new Dictionary<string, Customer>(StringComparer.OrdinalIgnoreCase);
        foreach (CustomerRecord record in snapshot.Customers)
        {
            var customer = new Customer(record.Id, record.Name, record.Email, record.Phone);
            customers.Add(customer.Id, customer);
            register.RestoreCustomer(customer);
        }

        foreach (RequestRecord record in snapshot.Requests)
        {
            register.RestoreRequest(ToRequest(record, customers[record.CustomerId]));
        }

        foreach (DeliveryRecord record in snapshot.Deliveries)
        {
            DeliveryRequest request = register.FindRequest(record.RequestReference)
                ?? throw new InvalidDataException($"Delivery {record.Id} refers to unknown request {record.RequestReference}.");
            register.RestoreDelivery(ToDelivery(record, request, roster));
        }

        return register;
    }

    private static StaffRecord ToRecord(DeliveryStaff staff)
    {
        return new StaffRecord(
            staff.Id,
            staff.Name,
            staff.Phone,
            staff.Email,
            staff.IsActive,
            staff.JoinedOn,
            staff.LeftOn,
            staff.Availability.Periods
                .Select(period => new AvailabilityPeriodRecord(period.Id, period.Window.Start, period.Window.End))
                .ToList());
    }

    private static RequestRecord ToRecord(DeliveryRequest request)
    {
        DeliveryQuote? quote = request.Quote;
        return new RequestRecord(
            request.Reference,
            request.Customer.Id,
            ToRecord(request.PickupAddress),
            ToRecord(request.DestinationAddress),
            request.PreferredPickupDate,
            request.SpecialInstructions,
            request.Channel,
            request.SubmittedAt,
            request.Parcels
                .Select(parcel => new ParcelRecord(
                    parcel.Id, parcel.Description, parcel.WeightKg, parcel.IsFragile, parcel.CollectedAt, parcel.DeliveredAt))
                .ToList(),
            quote is null
                ? null
                : new QuoteRecord(
                    quote.Id,
                    quote.Cost,
                    quote.Window.Start,
                    quote.Window.End,
                    quote.PreparedAt,
                    quote.Revision,
                    quote.Response,
                    quote.RespondedAt));
    }

    private static DeliveryRecord ToRecord(Delivery delivery)
    {
        Payment? payment = delivery.Payment;
        return new DeliveryRecord(
            delivery.Id,
            delivery.Request.Reference,
            delivery.CreatedAt,
            delivery.Status,
            delivery.AllocatedStaff?.Id,
            payment is null
                ? null
                : new PaymentRecord(payment.Id, payment.Amount, payment.Method, payment.ReceivedByStaffId, payment.ReceivedAt),
            delivery.PickedUpAt,
            delivery.DeliveredAt,
            delivery.ReceivedBy,
            delivery.History.ToList());
    }

    private static AddressRecord ToRecord(Address address) => new(address.Street, address.Suburb, address.Postcode);

    private static Address ToAddress(AddressRecord record) => Address.Create(record.Street, record.Suburb, record.Postcode, "Stored");

    private static DeliveryRequest ToRequest(RequestRecord record, Customer customer)
    {
        List<Parcel> parcels = record.Parcels
            .Select((parcel, index) => new Parcel(parcel.Id, parcel.Description, parcel.WeightKg, parcel.IsFragile, index + 1)
            {
                CollectedAt = parcel.CollectedAt,
                DeliveredAt = parcel.DeliveredAt,
            })
            .ToList();

        var request = new DeliveryRequest(
            record.Reference,
            customer,
            ToAddress(record.Pickup),
            ToAddress(record.Destination),
            record.PreferredPickupDate,
            record.SpecialInstructions,
            parcels,
            record.Channel,
            record.SubmittedAt);

        if (record.Quote is { } quote)
        {
            request.Quote = new DeliveryQuote(
                quote.Id, request, new QuoteTerms(quote.Cost, quote.EstimatedPickup, quote.EstimatedDelivery), quote.PreparedAt)
            {
                Revision = quote.Revision,
                Response = quote.Response,
                RespondedAt = quote.RespondedAt,
            };
        }

        return request;
    }

    private static Delivery ToDelivery(DeliveryRecord record, DeliveryRequest request, StaffRoster roster)
    {
        var delivery = new Delivery(record.Id, request, record.CreatedAt)
        {
            Status = record.Status,
            PickedUpAt = record.PickedUpAt,
            DeliveredAt = record.DeliveredAt,
            ReceivedBy = record.ReceivedBy,
        };

        foreach (DeliveryEvent entry in record.History)
        {
            delivery.Record(entry.At, entry.Description, entry.IsIssue);
        }

        if (record.AllocatedStaffId is not null)
        {
            DeliveryStaff staff = roster.Find(record.AllocatedStaffId)
                ?? throw new InvalidDataException($"Delivery {record.Id} refers to unknown staff member {record.AllocatedStaffId}.");
            delivery.AllocatedStaff = staff;
            staff.AssignDelivery(delivery);
        }

        if (record.Payment is { } payment)
        {
            delivery.Payment = new Payment(
                payment.Id, delivery, payment.Amount, payment.Method, payment.ReceivedByStaffId, payment.ReceivedAt);
        }

        return delivery;
    }
}

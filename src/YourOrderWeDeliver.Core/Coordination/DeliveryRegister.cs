using System.Globalization;
using YourOrderWeDeliver.Core.Common;
using YourOrderWeDeliver.Core.Domain;

namespace YourOrderWeDeliver.Core.Coordination;

/// <summary>
/// The system-wide collection and lookup point for customers, delivery requests and deliveries
/// (A2 CRC 5.11). It creates requests and, when a quote is accepted, the operational delivery.
/// </summary>
public sealed class DeliveryRegister
{
    private readonly Dictionary<string, Customer> _customers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DeliveryRequest> _requests = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Delivery> _deliveries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates an empty register for the given service area.</summary>
    public DeliveryRegister(ServiceArea serviceArea)
    {
        ServiceArea = serviceArea ?? throw new ArgumentNullException(nameof(serviceArea));
    }

    /// <summary>Gets the area within which pickups and destinations are accepted.</summary>
    public ServiceArea ServiceArea { get; }

    /// <summary>Gets all known customers.</summary>
    public IEnumerable<Customer> Customers => _customers.Values;

    /// <summary>Gets all requests, newest first (R1).</summary>
    public IEnumerable<DeliveryRequest> Requests => _requests.Values.OrderByDescending(request => request.SubmittedAt);

    /// <summary>Gets all deliveries, current and historical, newest first (R2).</summary>
    public IEnumerable<Delivery> Deliveries => _deliveries.Values.OrderByDescending(delivery => delivery.CreatedAt);

    /// <summary>Gets accepted deliveries that have no staff member allocated (A1 T4 subtask 1).</summary>
    public IEnumerable<Delivery> DeliveriesAwaitingAllocation =>
        _deliveries.Values.Where(d => d.Status == DeliveryStatus.AwaitingAllocation).OrderBy(d => d.Window.Start);

    /// <summary>Gets deliveries allocated but not yet picked up, which may still be reassigned.</summary>
    public IEnumerable<Delivery> DeliveriesAllocatedNotPickedUp =>
        _deliveries.Values.Where(d => d.Status == DeliveryStatus.Allocated).OrderBy(d => d.Window.Start);

    internal int LastCustomerNumber { get; set; }

    internal int LastRequestNumber { get; set; } = 100000;

    internal int LastDeliveryNumber { get; set; }

    /// <summary>
    /// Validates and records a new delivery request (A1 T1). A returning customer, recognised by
    /// email, is linked to the existing <see cref="Customer"/> instead of creating a duplicate.
    /// Nothing is changed unless every field is valid.
    /// </summary>
    public DeliveryRequest RecordRequest(RequestDetails details, RequestChannel channel, DateTime now)
    {
        PreparedRequest prepared = Prepare(details, now);

        Customer? customer = _customers.Values.FirstOrDefault(
            existing => string.Equals(existing.Email, prepared.Email, StringComparison.OrdinalIgnoreCase));
        if (customer is null)
        {
            string customerId = string.Create(CultureInfo.InvariantCulture, $"CUS-{LastCustomerNumber + 1:0000}");
            customer = new Customer(customerId, prepared.Name, prepared.Email, prepared.Phone);
            LastCustomerNumber++;
            _customers.Add(customer.Id, customer);
        }
        

        var request = new DeliveryRequest(
            prepared.Reference,
            customer,
            prepared.Pickup,
            prepared.Destination,
            details.PreferredPickupDate,
            prepared.Instructions,
            prepared.Parcels,
            channel,
            now);
        LastRequestNumber++;
        customer.AddRequest(request);
        _requests.Add(request.Reference, request);
        return request;
    }

    /// <summary>
    /// Applies every check of <see cref="RecordRequest"/> without recording anything, so the user can
    /// review a valid request before submitting it (A1 T1 subtask 4).
    /// </summary>
    public void ValidateRequest(RequestDetails details, DateTime now) => Prepare(details, now);

    /// <summary>Locates a request by reference for staff (R3).</summary>
    public DeliveryRequest? FindRequest(string? reference)
    {
        return reference is not null && _requests.TryGetValue(reference.Trim(), out DeliveryRequest? request) ? request : null;
    }

    /// <summary>
    /// Locates a request for a customer. The reference alone is not enough (A1 QR3.3): the
    /// customer's email or phone number must match as well.
    /// </summary>
    public DeliveryRequest? FindRequestForCustomer(string? reference, string? contact)
    {
        DeliveryRequest? request = FindRequest(reference);
        return request is not null && request.Customer.IsIdentifiedBy(contact) ? request : null;
    }

    /// <summary>Locates a delivery by its identifier or by its request reference (R4).</summary>
    public Delivery? FindDelivery(string? idOrReference)
    {
        if (string.IsNullOrWhiteSpace(idOrReference))
        {
            return null;
        }

        string key = idOrReference.Trim();
        return _deliveries.TryGetValue(key, out Delivery? delivery) ? delivery : FindRequest(key)?.Delivery;
    }

    /// <summary>
    /// Records the customer's response to the quote (A1 T3). On acceptance the operational
    /// <see cref="Delivery"/> is created and registered; on decline the request is kept but goes no further.
    /// </summary>
    /// <returns>The new delivery, or <c>null</c> when the quote was declined.</returns>
    public Delivery? RecordQuoteResponse(DeliveryRequest request, bool accepted, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(request);
        DeliveryQuote quote = request.Quote
            ?? throw new BusinessRuleException(
                $"No quote has been prepared yet for request {request.Reference}. Please wait for our office to send a quote.");
        quote.RecordResponse(accepted, now);
        if (!accepted)
        {
            return null;
        }

        string deliveryId = string.Create(CultureInfo.InvariantCulture, $"DEL-{LastDeliveryNumber + 1:000000}");
        var delivery = new Delivery(deliveryId, request, now);
        LastDeliveryNumber++;
        delivery.Record(now, $"Quote accepted by the customer. Delivery created for {DisplayFormat.Window(delivery.Window)}.");
        request.Delivery = delivery;
        _deliveries.Add(delivery.Id, delivery);
        return delivery;
    }

    /// <summary>Gets deliveries ever allocated to and still held by the staff member.</summary>
    public IEnumerable<Delivery> DeliveriesFor(DeliveryStaff staff)
    {
        return _deliveries.Values.Where(delivery => ReferenceEquals(delivery.AllocatedStaff, staff));
    }

    /// <summary>Gets deliveries completed in [<paramref name="from"/>, <paramref name="to"/>) (R6).</summary>
    public IEnumerable<Delivery> CompletedDeliveriesBetween(DateTime from, DateTime to)
    {
        return _deliveries.Values.Where(
            delivery => delivery.Status == DeliveryStatus.Delivered && delivery.DeliveredAt >= from && delivery.DeliveredAt < to);
    }

    /// <summary>Gets requests recorded in [<paramref name="from"/>, <paramref name="to"/>).</summary>
    public IEnumerable<DeliveryRequest> RequestsSubmittedBetween(DateTime from, DateTime to)
    {
        return _requests.Values.Where(request => request.SubmittedAt >= from && request.SubmittedAt < to);
    }

    internal void RestoreCustomer(Customer customer) => _customers.Add(customer.Id, customer);

    private PreparedRequest Prepare(RequestDetails details, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(details);
        string name = Require.PersonName(details.CustomerName, "Customer name", 100);
        string email = Require.Email(details.CustomerEmail);
        string phone = Require.Phone(details.CustomerPhone);
        Customer? existingCustomer = _customers.Values.FirstOrDefault(
            customer => string.Equals(customer.Email, email, StringComparison.OrdinalIgnoreCase));

        if (existingCustomer is not null)
        {
            bool detailsMatch =
                string.Equals(existingCustomer.Name, name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(existingCustomer.Phone, phone, StringComparison.Ordinal);

            Require.That(
                detailsMatch,
                "The customer details do not match the existing record for this email address. "
                + "Check the name and phone number, or contact the office for assistance. "
                + "Existing customer details have not been changed.");
        }
        Address pickup = Address.Create(details.Pickup.Street, details.Pickup.Suburb, details.Pickup.Postcode, "Pickup");
        Address destination = Address.Create(
            details.Destination.Street, details.Destination.Suburb, details.Destination.Postcode, "Destination");
        DeliveryRequest.ValidateLocations(pickup, destination, ServiceArea);
        DeliveryRequest.ValidatePreferredPickupDate(details.PreferredPickupDate, now);
        string? instructions = DeliveryRequest.NormaliseInstructions(details.SpecialInstructions);
        Require.That(
            details.Parcels.Count is >= 1 and <= DeliveryRequest.MaxParcels,
            $"A delivery request must contain between 1 and {DeliveryRequest.MaxParcels} parcels.");

        string reference = string.Create(CultureInfo.InvariantCulture, $"REQ-{LastRequestNumber + 1}");
        List<Parcel> parcels = details.Parcels
            .Select((parcel, index) => new Parcel(
                $"{reference}-P{index + 1}", parcel.Description, parcel.WeightKg, parcel.IsFragile, index + 1))
            .ToList();
        return new PreparedRequest(reference, name, email, phone, pickup, destination, instructions, parcels);
    }

    private sealed record PreparedRequest(
        string Reference,
        string Name,
        string Email,
        string Phone,
        Address Pickup,
        Address Destination,
        string? Instructions,
        IReadOnlyList<Parcel> Parcels);

    internal void RestoreRequest(DeliveryRequest request)
    {
        request.Customer.AddRequest(request);
        _requests.Add(request.Reference, request);
    }

    internal void RestoreDelivery(Delivery delivery)
    {
        delivery.Request.Delivery = delivery;
        _deliveries.Add(delivery.Id, delivery);
    }
}

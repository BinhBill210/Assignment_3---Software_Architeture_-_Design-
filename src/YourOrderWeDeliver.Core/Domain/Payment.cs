namespace YourOrderWeDeliver.Core.Domain;

/// <summary>
/// Data-holder: payment received at pickup (A2 CRC 5.6). Created only by <see cref="Delivery.RecordPickup"/>;
/// it is never changed afterwards so the record of what was collected stays trustworthy.
/// </summary>
public sealed class Payment
{
    internal Payment(string id, Delivery delivery, decimal amount, PaymentMethod method, string receivedByStaffId, DateTime receivedAt)
    {
        Id = id;
        Delivery = delivery;
        Amount = amount;
        Method = method;
        ReceivedByStaffId = receivedByStaffId;
        ReceivedAt = receivedAt;
    }

    /// <summary>Gets the identifier, for example "DEL-000001-PAY".</summary>
    public string Id { get; }

    /// <summary>Gets the delivery the payment was received for (R1).</summary>
    public Delivery Delivery { get; }

    /// <summary>Gets the amount received, equal to the accepted quote cost.</summary>
    public decimal Amount { get; }

    /// <summary>Gets how the customer paid.</summary>
    public PaymentMethod Method { get; }

    /// <summary>Gets the identifier of the staff member who received the payment.</summary>
    public string ReceivedByStaffId { get; }

    /// <summary>Gets when the payment was received.</summary>
    public DateTime ReceivedAt { get; }
}

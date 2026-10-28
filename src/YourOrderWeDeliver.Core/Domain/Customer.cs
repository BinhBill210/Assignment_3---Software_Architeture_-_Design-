using YourOrderWeDeliver.Core.Common;

namespace YourOrderWeDeliver.Core.Domain;

/// <summary>
/// A customer who requests delivery services (A2 CRC 5.1). Customers do not hold accounts
/// (A2 assumption A8); a returning customer is recognised by email address.
/// </summary>
public sealed class Customer
{
    private readonly List<DeliveryRequest> _requests = [];

    internal Customer(string id, string name, string email, string phone)
    {
        Id = id;
        Name = Require.PersonName(name, "Customer name", 100);
        Email = Require.Email(email);
        Phone = Require.Phone(phone);
    }

    /// <summary>Gets the system-assigned identifier, for example "CUS-0001".</summary>
    public string Id { get; }

    /// <summary>Gets the customer's full name.</summary>
    public string Name { get; private set; }

    /// <summary>Gets the customer's email address (lower case).</summary>
    public string Email { get; }

    /// <summary>Gets the customer's phone number without separators.</summary>
    public string Phone { get; private set; }

    /// <summary>Gets the delivery requests made by this customer (R2).</summary>
    public IReadOnlyList<DeliveryRequest> Requests => _requests;

    /// <summary>Determines whether an email address or phone number identifies this customer.</summary>
    public bool IsIdentifiedBy(string? contact)
    {
        if (string.IsNullOrWhiteSpace(contact))
        {
            return false;
        }

        string trimmed = contact.Trim();
        return string.Equals(trimmed, Email, StringComparison.OrdinalIgnoreCase)
            || string.Equals(Require.NormalisePhone(trimmed), Phone, StringComparison.Ordinal);
    }

    internal void UpdateContactDetails(string name, string phone)
    {
        Name = Require.PersonName(name, "Customer name", 100);
        Phone = Require.Phone(phone);
    }

    internal void AddRequest(DeliveryRequest request) => _requests.Add(request);
}

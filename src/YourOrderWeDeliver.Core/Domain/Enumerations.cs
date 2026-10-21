namespace YourOrderWeDeliver.Core.Domain;

/// <summary>Stage of a <see cref="DeliveryRequest"/> before it becomes an operational delivery.</summary>
public enum RequestStatus
{
    /// <summary>Recorded and waiting for office staff to prepare a quote.</summary>
    Submitted,

    /// <summary>A quote has been issued and is waiting for the customer's response.</summary>
    Quoted,

    /// <summary>The customer accepted the quote; a <see cref="Delivery"/> exists.</summary>
    Accepted,

    /// <summary>The customer declined the quote; the request does not proceed.</summary>
    Declined,
}

/// <summary>Customer response recorded against a <see cref="DeliveryQuote"/>.</summary>
public enum QuoteResponse
{
    /// <summary>No response has been recorded yet.</summary>
    Pending,

    /// <summary>The customer accepted the quoted service.</summary>
    Accepted,

    /// <summary>The customer declined the quoted service.</summary>
    Declined,
}

/// <summary>Lifecycle state of an operational <see cref="Delivery"/>.</summary>
public enum DeliveryStatus
{
    /// <summary>Accepted but no delivery staff member is currently allocated.</summary>
    AwaitingAllocation,

    /// <summary>A delivery staff member is allocated and pickup has not happened yet.</summary>
    Allocated,

    /// <summary>All parcels and the payment have been collected.</summary>
    PickedUp,

    /// <summary>All parcels have been handed over at the destination.</summary>
    Delivered,
}

/// <summary>How a delivery request entered the system (A1 task T1 variants 1a and 1b).</summary>
public enum RequestChannel
{
    /// <summary>Submitted by the customer through the web application.</summary>
    CustomerOnline,

    /// <summary>Recorded by office staff on behalf of the customer.</summary>
    OfficeStaff,
}

/// <summary>Payment method recorded at pickup. No real transaction is processed.</summary>
public enum PaymentMethod
{
    /// <summary>Cash handed to the delivery staff member.</summary>
    Cash,

    /// <summary>Card payment on the staff member's device.</summary>
    Card,

    /// <summary>Bank transfer confirmed at pickup.</summary>
    BankTransfer,
}

/// <summary>Reporting period supported by <see cref="DeliveryStatistics"/> (A1 task T9).</summary>
public enum ReportingPeriod
{
    /// <summary>The selected calendar day.</summary>
    Day,

    /// <summary>The Monday-to-Sunday week containing the selected day.</summary>
    Week,

    /// <summary>The calendar month containing the selected day.</summary>
    Month,

    /// <summary>The calendar year containing the selected day.</summary>
    Year,

    /// <summary>From 1 January up to and including the selected day.</summary>
    YearToDate,
}

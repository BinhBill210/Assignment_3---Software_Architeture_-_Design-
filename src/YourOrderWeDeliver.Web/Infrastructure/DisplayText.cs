using System.Text;
using YourOrderWeDeliver.Core.Domain;

namespace YourOrderWeDeliver.Web.Infrastructure;

/// <summary>Turns enum values into the plain words used on screen (A1 QR4.5, Section 2.7 vocabulary).</summary>
public static class DisplayText
{
    /// <summary>Converts "AwaitingAllocation" to "Awaiting allocation".</summary>
    public static string Label(Enum value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string name = value switch
        {
            RequestChannel.CustomerOnline => "Customer (online)",
            RequestChannel.OfficeStaff => "Office staff",
            PaymentMethod.BankTransfer => "Bank transfer",
            ReportingPeriod.YearToDate => "Year to date",
            _ => value.ToString(),
        };

        if (name.Contains(' ', StringComparison.Ordinal))
        {
            return name;
        }

        var text = new StringBuilder(name.Length + 4);
        for (int index = 0; index < name.Length; index++)
        {
            char current = name[index];
            if (index > 0 && char.IsUpper(current))
            {
                text.Append(' ').Append(char.ToLowerInvariant(current));
            }
            else
            {
                text.Append(current);
            }
        }

        return text.ToString();
    }

    /// <summary>Bootstrap badge colour for a request status.</summary>
    public static string BadgeClass(RequestStatus status) => status switch
    {
        RequestStatus.Submitted => "text-bg-secondary",
        RequestStatus.Quoted => "text-bg-info",
        RequestStatus.Accepted => "text-bg-success",
        _ => "text-bg-dark",
    };

    /// <summary>Bootstrap badge colour for a delivery status.</summary>
    public static string BadgeClass(DeliveryStatus status) => status switch
    {
        DeliveryStatus.AwaitingAllocation => "text-bg-warning",
        DeliveryStatus.Allocated => "text-bg-primary",
        DeliveryStatus.PickedUp => "text-bg-info",
        _ => "text-bg-success",
    };
}

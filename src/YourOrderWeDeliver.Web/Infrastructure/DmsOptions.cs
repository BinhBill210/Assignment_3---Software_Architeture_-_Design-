using YourOrderWeDeliver.Core.Domain;

namespace YourOrderWeDeliver.Web.Infrastructure;

/// <summary>Settings read from the "DeliveryManagementSystem" section of appsettings.json.</summary>
public sealed class DmsOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "DeliveryManagementSystem";

    /// <summary>Gets or sets the JSON data file, relative to the application folder.</summary>
    public string DataFile { get; set; } = "App_Data/yowd-data.json";

    /// <summary>Gets or sets whether demo staff and availability are created when the data file is empty.</summary>
    public bool SeedDemoData { get; set; } = true;

    /// <summary>Gets or sets the service area display name.</summary>
    public string ServiceAreaName { get; set; } = "Greater Melbourne";

    /// <summary>Gets or sets comma-separated postcode ranges, for example "3000-3211,3335-3341".</summary>
    public string ServiceAreaPostcodes { get; set; } = string.Empty;

    /// <summary>Gets or sets the shared password delivery staff use with their staff ID (prototype only).</summary>
    public string DeliveryStaffPassword { get; set; } = "driver123";

    /// <summary>Gets or sets office and management accounts.</summary>
    public IReadOnlyList<OfficeAccount> Accounts { get; set; } = [];

    /// <summary>Builds the configured service area, falling back to Greater Melbourne.</summary>
    public ServiceArea CreateServiceArea()
    {
        if (string.IsNullOrWhiteSpace(ServiceAreaPostcodes))
        {
            return ServiceArea.GreaterMelbourne();
        }

        return new ServiceArea(
            ServiceAreaName,
            ServiceAreaPostcodes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(PostcodeRange.Parse));
    }
}

/// <summary>A configured office staff or manager sign-in.</summary>
public sealed class OfficeAccount
{
    /// <summary>Gets or sets the sign-in name.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Gets or sets the password (prototype only; production would store a salted hash).</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Gets or sets the name shown in the navigation bar.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Gets or sets <see cref="Roles.OfficeStaff"/> or <see cref="Roles.Manager"/>.</summary>
    public string Role { get; set; } = Roles.OfficeStaff;
}

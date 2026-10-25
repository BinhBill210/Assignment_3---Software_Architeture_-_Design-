using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using YourOrderWeDeliver.Core.Coordination;
using YourOrderWeDeliver.Core.Domain;

namespace YourOrderWeDeliver.Web.Infrastructure;

/// <summary>User roles. Customers use the system without signing in (A1 assumption A2).</summary>
public static class Roles
{
    /// <summary>Office staff: record requests, prepare quotes, allocate staff, maintain availability.</summary>
    public const string OfficeStaff = "OfficeStaff";

    /// <summary>Business owner or manager: everything office staff can do, plus statistics and staff records.</summary>
    public const string Manager = "Manager";

    /// <summary>Delivery staff: pickups, deliveries and their own availability.</summary>
    public const string DeliveryStaff = "DeliveryStaff";
}

/// <summary>Authorisation policy names (A1 QR3.1, QR3.2, QR3.4).</summary>
public static class Policies
{
    /// <summary>Office staff or manager.</summary>
    public const string Office = "Office";

    /// <summary>Manager only.</summary>
    public const string Manager = "Manager";

    /// <summary>Delivery staff only.</summary>
    public const string DeliveryStaff = "DeliveryStaff";

    /// <summary>Any signed-in staff member.</summary>
    public const string AnyStaff = "AnyStaff";
}

/// <summary>Helpers for reading the signed-in user.</summary>
public static class UserExtensions
{
    /// <summary>Claim holding the delivery staff identifier.</summary>
    public const string StaffIdClaim = "yowd:staff-id";

    /// <summary>Determines whether the user is office staff or a manager.</summary>
    public static bool IsOffice(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.IsInRole(Roles.OfficeStaff) || user.IsInRole(Roles.Manager);
    }

    /// <summary>Determines whether the user is signed in as delivery staff.</summary>
    public static bool IsDeliveryStaff(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.IsInRole(Roles.DeliveryStaff);
    }

    /// <summary>Gets the delivery staff identifier of the user, if any.</summary>
    public static string? StaffId(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.FindFirstValue(StaffIdClaim);
    }

    /// <summary>Gets a readable role name for the navigation bar.</summary>
    public static string RoleLabel(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.IsInRole(Roles.Manager))
        {
            return "Manager";
        }

        return user.IsInRole(Roles.OfficeStaff) ? "Office staff" : "Delivery staff";
    }
}

/// <summary>
/// Checks staff credentials. Office and manager accounts come from configuration; delivery staff
/// sign in with their staff ID, and only while their record is active (A2 assumption A5).
/// </summary>
public sealed class StaffAccountService(DmsOptions options, DeliveryManagementSystem system)
{
    /// <summary>Returns the signed-in principal, or <c>null</c> when the credentials are not valid.</summary>
    public ClaimsPrincipal? Authenticate(string? username, string? password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            return null;
        }

        string name = username.Trim();
        OfficeAccount? account = options.Accounts.FirstOrDefault(
            candidate => string.Equals(candidate.Username, name, StringComparison.OrdinalIgnoreCase));
        if (account is not null)
        {
            return PasswordMatches(account.Password, password)
                ? CreatePrincipal(account.Username, account.DisplayName, account.Role, staffId: null)
                : null;
        }

        DeliveryStaff? staff = system.Roster.Find(name);
        return staff is { IsActive: true } && PasswordMatches(options.DeliveryStaffPassword, password)
            ? CreatePrincipal(staff.Id, staff.Name, Roles.DeliveryStaff, staff.Id)
            : null;
    }

    private static bool PasswordMatches(string expected, string supplied)
    {
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied));
    }

    private static ClaimsPrincipal CreatePrincipal(string username, string displayName, string role, string? staffId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, username),
            new(ClaimTypes.Name, displayName),
            new(ClaimTypes.Role, role),
        };
        if (staffId is not null)
        {
            claims.Add(new Claim(UserExtensions.StaffIdClaim, staffId));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}

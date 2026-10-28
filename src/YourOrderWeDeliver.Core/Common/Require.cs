using System.Text.RegularExpressions;

namespace YourOrderWeDeliver.Core.Common;

/// <summary>
/// Input and precondition checks shared by the domain classes. Every failure is reported as a
/// <see cref="BusinessRuleException"/> so the user interface can show it next to the form.
/// </summary>
internal static partial class Require
{
    public static string Text(string? value, string fieldLabel, int maxLength)
    {
        string trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new BusinessRuleException($"{fieldLabel} is required. Please enter a value.");
        }

        if (trimmed.Length > maxLength)
        {
            throw new BusinessRuleException($"{fieldLabel} must be {maxLength} characters or fewer.");
        }

        return trimmed;
    }
    public static string PersonName(string? value, string fieldLabel, int maxLength)
    {
        string name = Text(value, fieldLabel, maxLength);

        if (!name.Any(char.IsLetter) || !PersonNamePattern().IsMatch(name))
        {
            throw new BusinessRuleException(
                $"{fieldLabel} must contain letters and may include spaces, "
                + "apostrophes, hyphens and periods. Numbers are not allowed.");
        }

        return name;
    }

    public static string? OptionalText(string? value, string fieldLabel, int maxLength)
    {
        return string.IsNullOrWhiteSpace(value) ? null : Text(value, fieldLabel, maxLength);
    }

    public static string Email(string? value, string fieldLabel = "Email address")
    {
        string email = Text(value, fieldLabel, 254);
        if (!EmailPattern().IsMatch(email))
        {
            throw new BusinessRuleException($"{fieldLabel} is not a valid email address, for example name@example.com.");
        }

        return email.ToLowerInvariant();
    }

    public static string Phone(string? value, string fieldLabel = "Phone number")
    {
        string phone = NormalisePhone(Text(value, fieldLabel, 20));
        if (!AustralianPhonePattern().IsMatch(phone))
        {
            throw new BusinessRuleException(
                $"{fieldLabel} must be an Australian number, for example 0412 345 678 or 03 9214 8000.");
        }

        return phone;
    }

    public static string NormalisePhone(string value)
    {
        return PhoneSeparators().Replace(value.Trim(), string.Empty);
    }

    public static void That(bool condition, string message)
    {
        if (!condition)
        {
            throw new BusinessRuleException(message);
        }
    }
    [GeneratedRegex(@"\A[\p{L}\p{M} .'\u2019\u2010\u2011-]+\z")]
    private static partial Regex PersonNamePattern();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^(?:\+61|0)[2-478]\d{8}$")]
    private static partial Regex AustralianPhonePattern();

    [GeneratedRegex(@"[\s\-\(\)]")]
    private static partial Regex PhoneSeparators();
}

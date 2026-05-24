using System.Net.Mail;
using System.Numerics;

namespace BusinessLogic.Common;

/// <summary>
/// Tiny set of guard helpers shared by entities. Kept here (and not in each
/// entity) so the validation rules — and their error messages — stay consistent.
/// </summary>
internal static class DomainGuard
{
    public static string NotBlank(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainValidationException($"'{field}' is required.");

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new DomainValidationException(
                $"'{field}' cannot be longer than {maxLength} characters.");

        return trimmed;
    }

    public static string Email(string? value, string field, int maxLength)
    {
        var trimmed = NotBlank(value, field, maxLength);

        // MailAddress.TryCreate covers RFC-style validation well enough for
        // storage purposes; full RFC 5322 parsing is intentionally out of scope.
        if (!MailAddress.TryCreate(trimmed, out _))
            throw new DomainValidationException($"'{field}' is not a valid email address.");

        return trimmed;
    }

    /// <summary>
    /// Validates an optional text field: trims whitespace and enforces max length.
    /// Null or blank values are allowed and stored as an empty string.
    /// </summary>
    public static string OptionalText(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new DomainValidationException(
                $"'{field}' cannot be longer than {maxLength} characters.");

        return trimmed;
    }

    public static T NonNegative<T>(T value, string field)
        where T : struct, INumber<T>
    {
        if (value < T.Zero)
            throw new DomainValidationException($"'{field}' cannot be negative.");

        return value;
    }

    public static (DateTime Start, DateTime End) DateRange(
        DateTime start, DateTime end, string startField, string endField)
    {
        if (end < start)
            throw new DomainValidationException(
                $"'{endField}' must be on or after '{startField}'.");

        return (start, end);
    }
}

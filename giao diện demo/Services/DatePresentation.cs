using System.Globalization;

namespace giao_dien_demo.Services;

/// <summary>
/// Presentation-only date formatting. It never changes storage, parsing, timezone,
/// workflow predicates, or model binding transport formats.
/// </summary>
public static class DatePresentation
{
    public const string DateOnlyFormat = "dd/MM/yyyy";
    public const string DateTimeFormat = "dd/MM/yyyy HH:mm";
    public const string DateTimeWithSecondsFormat = "dd/MM/yyyy HH:mm:ss";

    public static string FormatDate(DateTime? value)
        => !value.HasValue || value.Value == DateTime.MinValue
            ? string.Empty
            : value.Value.ToString(DateOnlyFormat, CultureInfo.InvariantCulture);

    public static string FormatDateTime(DateTime? value, bool includeSeconds = false)
        => !value.HasValue || value.Value == DateTime.MinValue
            ? string.Empty
            : value.Value.ToString(includeSeconds ? DateTimeWithSecondsFormat : DateTimeFormat, CultureInfo.InvariantCulture);
}

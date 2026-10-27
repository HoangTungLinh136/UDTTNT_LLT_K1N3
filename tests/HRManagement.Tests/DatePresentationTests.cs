using giao_dien_demo.Services;
using Xunit;

namespace HRManagement.Tests;

public sealed class DatePresentationTests
{
    [Fact]
    public void SYS02_DATE_001_date_only_uses_dd_MM_yyyy()
    {
        Assert.Equal("19/06/2006", DatePresentation.FormatDate(new DateTime(2006, 6, 19)));
        Assert.DoesNotContain("06/19", DatePresentation.FormatDate(new DateTime(2006, 6, 19)));
        Assert.DoesNotContain("19/06/06", DatePresentation.FormatDate(new DateTime(2006, 6, 19)));
    }

    [Fact]
    public void SYS02_DATE_002_nullable_and_minimum_dates_are_empty()
    {
        Assert.Equal(string.Empty, DatePresentation.FormatDate(null));
        Assert.Equal(string.Empty, DatePresentation.FormatDate(DateTime.MinValue));
        Assert.Equal(string.Empty, DatePresentation.FormatDateTime(DateTime.MinValue));
    }

    [Fact]
    public void SYS02_DATE_003_date_time_preserves_meaningful_time()
    {
        var value = new DateTime(2026, 9, 19, 8, 30, 12);
        Assert.Equal("19/09/2026 08:30", DatePresentation.FormatDateTime(value));
        Assert.Equal("19/09/2026 08:30:12", DatePresentation.FormatDateTime(value, includeSeconds: true));
    }
}

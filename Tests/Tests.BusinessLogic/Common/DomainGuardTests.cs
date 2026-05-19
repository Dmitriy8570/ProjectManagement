using BusinessLogic.Common;

namespace Tests.BusinessLogic.Common;

public class DomainGuardTests
{
    // ---------- NotBlank ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void NotBlank_WithNullOrWhitespace_Throws(string? value)
    {
        Assert.Throws<DomainValidationException>(
            () => DomainGuard.NotBlank(value, "param", maxLength: 100));
    }

    [Fact]
    public void NotBlank_WithValidString_ReturnsTrimmedValue()
    {
        var result = DomainGuard.NotBlank("  valid  ", "param", maxLength: 100);
        Assert.Equal("valid", result);
    }

    [Fact]
    public void NotBlank_WithExactlyMaxLength_ReturnsValue()
    {
        var exactlyMax = new string('a', 100);

        var result = DomainGuard.NotBlank(exactlyMax, "param", maxLength: 100);

        Assert.Equal(exactlyMax, result);
    }

    [Fact]
    public void NotBlank_WhenLongerThanMaxLength_Throws()
    {
        var tooLong = new string('a', 101);

        Assert.Throws<DomainValidationException>(
            () => DomainGuard.NotBlank(tooLong, "param", maxLength: 100));
    }

    [Fact]
    public void NotBlank_ErrorMessage_ContainsFieldName()
    {
        var ex = Assert.Throws<DomainValidationException>(
            () => DomainGuard.NotBlank("", "myField", maxLength: 100));

        Assert.Contains("myField", ex.Message);
    }

    // ---------- Email ----------

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("first.last@example.com")]
    [InlineData("a.b+tag@sub.example.co.uk")]
    public void Email_WithValidAddress_ReturnsValue(string email)
    {
        var result = DomainGuard.Email(email, "email", maxLength: 100);

        Assert.Equal(email, result);
    }

    [Fact]
    public void Email_WithSurroundingWhitespace_ReturnsTrimmedValue()
    {
        var result = DomainGuard.Email("  user@example.com  ", "email", maxLength: 100);

        Assert.Equal("user@example.com", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Email_WithNullOrWhitespace_Throws(string? email)
    {
        Assert.Throws<DomainValidationException>(
            () => DomainGuard.Email(email, "email", maxLength: 100));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("@nodomain.com")]
    [InlineData("nouser@")]
    [InlineData("missing-at-sign.com")]
    [InlineData("user@.com")]
    public void Email_WithInvalidFormat_Throws(string email)
    {
        Assert.Throws<DomainValidationException>(
            () => DomainGuard.Email(email, "email", maxLength: 100));
    }

    [Fact]
    public void Email_WhenLongerThanMaxLength_Throws()
    {
        var longLocal = new string('a', 95);
        var tooLong = $"{longLocal}@example.com";

        Assert.Throws<DomainValidationException>(
            () => DomainGuard.Email(tooLong, "email", maxLength: 100));
    }

    // ---------- NonNegative ----------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public void NonNegative_WithZeroOrPositive_ReturnsValue(int value)
    {
        var result = DomainGuard.NonNegative(value, "number");

        Assert.Equal(value, result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(int.MinValue)]
    public void NonNegative_WithNegativeValue_Throws(int value)
    {
        Assert.Throws<DomainValidationException>(
            () => DomainGuard.NonNegative(value, "number"));
    }

    // ---------- DateRange ----------

    [Fact]
    public void DateRange_WithEndAfterStart_ReturnsBothDates()
    {
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 12, 31);

        var result = DomainGuard.DateRange(start, end, "start", "end");

        Assert.Equal(start, result.Start);
        Assert.Equal(end, result.End);
    }

    [Fact]
    public void DateRange_WithEndEqualsStart_IsAllowed()
    {
        var date = new DateTime(2024, 6, 1);

        var result = DomainGuard.DateRange(date, date, "start", "end");

        Assert.Equal(date, result.Start);
        Assert.Equal(date, result.End);
    }

    [Fact]
    public void DateRange_WhenEndBeforeStart_Throws()
    {
        var start = new DateTime(2024, 12, 31);
        var end = new DateTime(2024, 1, 1);

        Assert.Throws<DomainValidationException>(
            () => DomainGuard.DateRange(start, end, "start", "end"));
    }
}

using SequencingApi.Infrastructure.DataSource.Mmci;
using Xunit;

namespace SequencingApi.UnitTests;

/// <summary>
/// Decoding of MMCI's raw source text. Every case here is a spelling observed in the live tree — the
/// point of these tests is that a Czech-locale Windows tool writes numbers and dates in ways the
/// invariant parsers reject outright.
/// </summary>
public sealed class MmciSourceValuesTests
{
    [Theory]
    [InlineData("96,592", 96.592)]      // decimal comma
    [InlineData("96,592%", 96.592)]     // ...with a percent sign
    [InlineData("96.592", 96.592)]      // already invariant
    [InlineData("1001,43", 1001.43)]
    [InlineData("2,040", 2.04)]
    [InlineData("  812,5  ", 812.5)]
    [InlineData("\"640\"", 640d)]
    public void NumberParsesTheSpellingsTheReportsUse(string raw, double expected) =>
        Assert.Equal(expected, MmciSourceValues.Number(raw));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-")]
    [InlineData("N/A")]
    [InlineData("not a number")]
    [InlineData(null)]
    public void NumberTreatsUnparseableAsAbsent(string? raw) =>
        Assert.Null(MmciSourceValues.Number(raw));

    [Fact]
    public void IntegerRoundsAQuotedFractionalCount()
    {
        // Counts are sometimes written with a decimal part; they are still counts.
        Assert.Equal(4_200_000L, MmciSourceValues.Integer("4200000,00"));
        Assert.Equal(3L, MmciSourceValues.Integer("2,6"));
    }

    [Fact]
    public void ShortDateReadsTheRunFolderDatePart()
    {
        Assert.Equal(new DateOnly(2024, 1, 4), MmciSourceValues.ShortDate("240104"));
        Assert.Equal(new DateOnly(2019, 11, 28), MmciSourceValues.ShortDate("191128"));
    }

    [Theory]
    [InlineData("24010")]      // too short
    [InlineData("2401044")]    // too long
    [InlineData("241301")]     // month 13
    public void ShortDateRejectsAnythingNotSixDigitDate(string raw) =>
        Assert.Null(MmciSourceValues.ShortDate(raw));

    [Theory]
    [InlineData("1.1.2022", 2022, 1, 1)]
    [InlineData("31.12.2025", 2025, 12, 31)]
    [InlineData("2022-01-01", 2022, 1, 1)]
    public void DateReadsTheDayFirstSpellingsTheTableUses(string raw, int year, int month, int day) =>
        Assert.Equal(new DateOnly(year, month, day), MmciSourceValues.Date(raw));

    [Theory]
    [InlineData("PRAVDA", true)]
    [InlineData("pravda", true)]
    [InlineData("NEPRAVDA", false)]
    [InlineData("TRUE", true)]
    [InlineData("FALSE", false)]
    public void BooleanReadsCzechAndEnglish(string raw, bool expected) =>
        Assert.Equal(expected, MmciSourceValues.Boolean(raw));

    [Fact]
    public void BooleanTreatsAnUnknownWordAsNotStatedRatherThanFalse()
    {
        // The distinction matters: "not recorded" and "recorded as no" are different facts.
        Assert.Null(MmciSourceValues.Boolean("maybe"));
        Assert.Null(MmciSourceValues.Boolean(""));
    }

    [Fact]
    public void KeyValueSplitsAReportLineAndSkipsOnesWithoutASeparator()
    {
        Assert.Equal(("Average Coverage", "812,5"), MmciSourceValues.KeyValue("Average Coverage: 812,5"));
        Assert.Null(MmciSourceValues.KeyValue("[Alignment Statistics]"));
        Assert.Null(MmciSourceValues.KeyValue(": orphaned value"));
    }

    [Fact]
    public void LinesToleratesMixedCarriageReturnsAndLineFeeds()
    {
        var lines = MmciSourceValues.Lines("first\r\nsecond\rthird\nfourth");

        Assert.Equal(["first", "second", "third", "fourth"], lines);
    }

    [Fact]
    public void SymbolListSplitsOnEverySeparatorTheGeneCellMixes()
    {
        Assert.Equal(["BRCA1", "BRCA2", "TP53"], MmciSourceValues.SymbolList("BRCA1, BRCA2;TP53"));

        // Spaces are separators here, which is why this one cannot go through the number cleaner.
        Assert.Equal(["BRCA1", "BRCA2"], MmciSourceValues.SymbolList("BRCA1 BRCA2"));
        Assert.Empty(MmciSourceValues.SymbolList("  "));
    }

    [Theory]
    [InlineData("100ngr", 100)]
    [InlineData("200ng", 200)]
    [InlineData("120ngr", 120)]
    [InlineData("10-25ngr", 10)]        // a range yields its lower bound
    [InlineData("20-100ngr", 20)]
    [InlineData("100-500ngr", 100)]
    [InlineData("  300ngr ", 300)]
    public void QuantityReadsAnAmountWrittenWithItsUnit(string raw, int expected)
    {
        // Every value in the libraries table's input-amount column carries a unit, so a plain numeric
        // parse returned null for all of them and the column was empty for all 4414 rows.
        Assert.Equal(expected, MmciSourceValues.Quantity(raw));
    }

    [Theory]
    [InlineData("TSO500")]    // the table has one cell holding a panel name where an amount belongs
    [InlineData("ngr")]
    [InlineData("-")]
    [InlineData("")]
    [InlineData(null)]
    public void QuantityIsAbsentRatherThanZeroWhenNoAmountIsStated(string? raw) =>
        Assert.Null(MmciSourceValues.Quantity(raw));

    [Fact]
    public void QuantityDoesNotChangeHowPlainNumbersAreRead()
    {
        // Int32 still owns the ordinary case; Quantity exists only for the unit-bearing column.
        Assert.Equal(250, MmciSourceValues.Quantity("250"));
        Assert.Equal(250, MmciSourceValues.Int32("250"));
    }
}

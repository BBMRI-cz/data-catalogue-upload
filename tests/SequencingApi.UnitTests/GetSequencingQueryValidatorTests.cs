using SequencingApi.Application.Features.Sequencing;
using Xunit;

namespace SequencingApi.UnitTests;

/// <summary>
/// The request validator behind <c>GET /sequencing</c>. It exists to keep a malformed request from
/// reading like a well-formed one with no answer: without it, a caller who omitted the parameter
/// would get an empty 200 that is indistinguishable from "this patient has no sequencing".
/// </summary>
public sealed class GetSequencingQueryValidatorTests
{
    private readonly GetSequencingQueryValidator _validator = new();

    [Fact]
    public void AcceptsAPredictiveNumber() =>
        Assert.True(_validator.Validate(new GetSequencingQuery("4-21")).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsAMissingPredictiveNumber(string predictiveNumber)
    {
        var result = _validator.Validate(new GetSequencingQuery(predictiveNumber));

        Assert.False(result.IsValid);
        Assert.Equal(nameof(GetSequencingQuery.PredictiveNumber), Assert.Single(result.Errors).PropertyName);
    }
}

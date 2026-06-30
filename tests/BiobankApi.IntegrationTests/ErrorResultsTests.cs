using BiobankApi.Web.Http;
using ErrorOr;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Xunit;

namespace BiobankApi.IntegrationTests;

/// <summary>
/// Unit coverage for the API edge's <see cref="ErrorResults.Problem"/> mapping (pure logic, no host).
/// Lives here because only this test project references <c>BiobankApi.Web</c> (where it is internal).
/// </summary>
public sealed class ErrorResultsTests
{
    [Fact]
    public void EmptyErrorsMapToGenericProblem()
    {
        var result = ErrorResults.Problem([]);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
    }

    [Fact]
    public void AllValidationErrorsMapToValidationProblemGroupedByCode()
    {
        var errors = new List<Error>
        {
            Error.Validation("Patient.BirthYear", "out of range"),
            Error.Validation("Patient.BirthYear", "also bad"),
            Error.Validation("Sample.SampleId", "empty"),
        };

        var result = ErrorResults.Problem(errors);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        var details = Assert.IsType<HttpValidationProblemDetails>(problem.ProblemDetails);
        Assert.Equal(2, details.Errors["Patient.BirthYear"].Length);
        Assert.Single(details.Errors["Sample.SampleId"]);
    }

    [Fact]
    public void NotFoundErrorMapsTo404()
    {
        var result = ErrorResults.Problem([Error.NotFound("Patient.NotFound", "no such patient")]);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
        Assert.Equal("no such patient", problem.ProblemDetails.Detail);
    }

    [Fact]
    public void UnexpectedErrorMapsTo500()
    {
        var result = ErrorResults.Problem([Error.Unexpected("Boom", "kaboom")]);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
    }
}

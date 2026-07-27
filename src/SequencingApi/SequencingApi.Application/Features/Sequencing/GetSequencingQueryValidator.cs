using FluentValidation;

namespace SequencingApi.Application.Features.Sequencing;

/// <summary>
/// Request-shape validation for <see cref="GetSequencingQuery"/>: a caller that omitted the
/// predictive number asked a malformed question, and must get a 400 rather than an empty answer that
/// looks like "this patient has no sequencing".
/// </summary>
/// <remarks>
/// Registered automatically by <c>AddValidatorsFromAssembly</c> and run by
/// <c>ValidationBehavior&lt;,&gt;</c> before the handler. This complements the domain's
/// <c>Create(...)</c> invariants rather than replacing them — there is no aggregate here to validate,
/// only the request.
/// </remarks>
public sealed class GetSequencingQueryValidator : AbstractValidator<GetSequencingQuery>
{
    public GetSequencingQueryValidator() =>
        RuleFor(query => query.PredictiveNumber)
            .NotEmpty()
            .WithMessage("predictive_number is required");
}

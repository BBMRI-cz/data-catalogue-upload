using ErrorOr;

namespace Uploader.Application.Abstractions;

/// <summary>
/// The two ways reading a source can fail. They are told apart in the run summary because they
/// mean different things to whoever reads it: one is an outage to chase, the other is a payload
/// to fix.
/// </summary>
public static class SourceErrors
{
    /// <summary>The source could not be reached at all - refused, timed out, or answered non-2xx.</summary>
    public const string UnavailableCode = "Source.Unavailable";

    /// <summary>The source answered, but with something that could not be read.</summary>
    public const string InvalidCode = "Source.Invalid";

    public static Error Unavailable(string source, string description) =>
        Error.Unexpected(UnavailableCode, $"{source} is unavailable: {description}");

    public static Error Invalid(string source, string description) =>
        Error.Failure(InvalidCode, $"{source} returned an unreadable payload: {description}");
}

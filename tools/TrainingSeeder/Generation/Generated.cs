using ErrorOr;

namespace TrainingSeeder.Generation;

internal static class Generated
{
    /// <summary>
    /// Unwraps a domain factory's result. The generator only builds what the domain accepts, so a
    /// rejection means a domain rule changed under it: fail loudly, naming the rule, and let the CI
    /// test catch it before anyone runs the stack.
    /// </summary>
    public static T Valid<T>(ErrorOr<T> result) =>
        result.IsError
            ? throw new InvalidOperationException(
                "A generated record broke a domain rule; update the generator: "
                + string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}")))
            : result.Value;
}

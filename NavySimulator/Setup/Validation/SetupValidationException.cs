namespace NavySimulator.Setup;

public class SetupValidationException : Exception
{
    public IReadOnlyList<string> Errors { get; }

    public SetupValidationException(List<string> errors)
        : base(BuildMessage(errors))
    {
        Errors = errors;
    }

    private static string BuildMessage(List<string> errors)
    {
        var lines = errors.Select(error => $"- {error}");
        return "Setup validation failed:" + Environment.NewLine + string.Join(Environment.NewLine, lines);
    }
}



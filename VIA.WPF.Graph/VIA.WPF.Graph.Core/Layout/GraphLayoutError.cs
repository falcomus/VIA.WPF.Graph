namespace VIA.WPF.Graph.Core.Layout;

/// <summary>
/// Describes a controlled layout failure without throwing through the UI layer.
/// </summary>
public sealed record GraphLayoutError
{
    public GraphLayoutError(string message, string? details = null, string? exceptionType = null)
    {
        Message = RequireText(message, nameof(message));
        Details = NormalizeOptionalText(details);
        ExceptionType = NormalizeOptionalText(exceptionType);
    }

    public string Message { get; }

    public string? Details { get; }

    public string? ExceptionType { get; }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be null, empty or whitespace.", parameterName);
        }

        return value;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

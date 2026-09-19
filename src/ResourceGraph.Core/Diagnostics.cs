using System.Collections.Immutable;

namespace ResourceGraph.Core;

/// <summary>Severity for a graph diagnostic.</summary>
public enum DiagnosticSeverity
{
    /// <summary>Informational diagnostic.</summary>
    Info,

    /// <summary>Recoverable warning.</summary>
    Warning,

    /// <summary>Failure that prevented one operation.</summary>
    Error
}

/// <summary>An explicit diagnostic associated with a graph operation.</summary>
public sealed record GraphDiagnostic
{
    /// <summary>Creates a validated diagnostic.</summary>
    public GraphDiagnostic(
        string code,
        string message,
        DiagnosticSeverity severity,
        string? resourceIdentity = null,
        int? position = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A diagnostic code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("A diagnostic message is required.", nameof(message));
        }

        Code = code;
        Message = message;
        Severity = severity;
        ResourceIdentity = resourceIdentity;
        Position = position;
    }

    /// <summary>Gets the stable diagnostic code.</summary>
    public string Code { get; }

    /// <summary>Gets the diagnostic message.</summary>
    public string Message { get; }

    /// <summary>Gets the severity.</summary>
    public DiagnosticSeverity Severity { get; }

    /// <summary>Gets the optional resource identity.</summary>
    public string? ResourceIdentity { get; }

    /// <summary>Gets the optional membership position.</summary>
    public int? Position { get; }
}

/// <summary>Boundaries applied while expanding a graph.</summary>
public sealed record ExpansionLimits
{
    /// <summary>Creates validated expansion limits.</summary>
    public ExpansionLimits(
        int maxDepth = 8,
        int maxMembersPerBundle = 1_000,
        int maxExpandedItems = 10_000,
        long maxBytes = 8 * 1024 * 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxMembersPerBundle);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxExpandedItems);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

        MaxDepth = maxDepth;
        MaxMembersPerBundle = maxMembersPerBundle;
        MaxExpandedItems = maxExpandedItems;
        MaxBytes = maxBytes;
    }

    /// <summary>Gets a conservative default policy.</summary>
    public static ExpansionLimits Default { get; } = new();

    /// <summary>Gets the maximum nested bundle depth.</summary>
    public int MaxDepth { get; }

    /// <summary>Gets the maximum members read from one bundle.</summary>
    public int MaxMembersPerBundle { get; }

    /// <summary>Gets the maximum unique resources emitted.</summary>
    public int MaxExpandedItems { get; }

    /// <summary>Gets the maximum source bytes an adapter may consume.</summary>
    public long MaxBytes { get; }
}

/// <summary>A result containing a value and explicit diagnostics.</summary>
public sealed record OperationResult<T>(T? Value, ImmutableArray<GraphDiagnostic> Diagnostics)
{
    /// <summary>Gets whether no error diagnostic was reported.</summary>
    public bool IsSuccess => !Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

    /// <summary>Creates a result with a materialized diagnostic list.</summary>
    public OperationResult(T? value, IEnumerable<GraphDiagnostic>? diagnostics = null)
        : this(value, (diagnostics ?? Array.Empty<GraphDiagnostic>()).ToImmutableArray())
    {
    }
}

/// <summary>Validates already-constructed graph models.</summary>
public static class GraphValidator
{
    /// <summary>Returns structural diagnostics for a bundle.</summary>
    public static ImmutableArray<GraphDiagnostic> Validate(Bundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        var diagnostics = ImmutableArray.CreateBuilder<GraphDiagnostic>();
        if (bundle.Members.Length == 0)
        {
            diagnostics.Add(new GraphDiagnostic(
                "bundle.empty",
                "A bundle has no memberships.",
                DiagnosticSeverity.Info));
        }

        foreach (var membership in bundle.Members)
        {
            if (membership.Resource.Kind == ResourceReferenceKind.Unknown)
            {
                diagnostics.Add(new GraphDiagnostic(
                    "resource.unknown",
                    "The resource is preserved without a known decoder.",
                    DiagnosticSeverity.Warning,
                    membership.Resource.Identity,
                    membership.Position));
            }
        }

        return diagnostics.ToImmutable();
    }
}

namespace ResourceGraph.Aspire;

/// <summary>Describes the optional local topology without provisioning resources.</summary>
public interface IResourceGraphTopology
{
    /// <summary>Gets the topology name.</summary>
    string Name { get; }

    /// <summary>Gets whether a local PDS or relay is required by the topology.</summary>
    bool RequiresAtProtocolInfrastructure { get; }
}

/// <summary>A placeholder topology descriptor for future Aspire integration.</summary>
public sealed class ReferenceTopology : IResourceGraphTopology
{
    /// <inheritdoc />
    public string Name => "resource-graph-reference";

    /// <inheritdoc />
    public bool RequiresAtProtocolInfrastructure => false;
}

using System.Text.Json;

namespace ResourceGraph.Bootstrap.Tests;

public sealed class BootstrapTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void CheckedInJsonFilesAreSyntacticallyValid()
    {
        var jsonFiles = Directory
            .EnumerateFiles(RepositoryRoot, "*.json", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.NotEmpty(jsonFiles);

        foreach (var path in jsonFiles)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            Assert.NotEqual(JsonValueKind.Undefined, document.RootElement.ValueKind);
        }
    }

    [Fact]
    public void RequiredDocumentationAndLexiconPathsExist()
    {
        var requiredPaths = new[]
        {
            "README.md",
            "specs/SPEC.md",
            "specs/BUNDLE.md",
            "specs/REFERENCES.md",
            "specs/OPML-PROFILE.md",
            "specs/CONFORMANCE.md",
            "specs/SECURITY.md",
            "docs/docfx.json",
            "docs/index.md",
            "lexicons/me/lqdev/resourcegraph/temp/bundle.json",
            "lexicons/me/lqdev/resourcegraph/temp/defs.json"
        };

        foreach (var relativePath in requiredPaths)
        {
            Assert.True(File.Exists(Path.Combine(RepositoryRoot, relativePath)), relativePath);
        }
    }

    [Fact]
    public void DraftLexiconsUseTheExpectedIdentifiers()
    {
        var bundle = Parse("lexicons/me/lqdev/resourcegraph/temp/bundle.json");
        var defs = Parse("lexicons/me/lqdev/resourcegraph/temp/defs.json");

        Assert.Equal("me.lqdev.resourcegraph.temp.bundle", bundle.RootElement.GetProperty("id").GetString());
        Assert.Equal("me.lqdev.resourcegraph.temp.defs", defs.RootElement.GetProperty("id").GetString());
        Assert.Contains("Draft", bundle.RootElement.GetProperty("description").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Draft", defs.RootElement.GetProperty("description").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MembershipUsesAnInlineUnionOfReusableObjectDefinitions()
    {
        var defs = Parse("lexicons/me/lqdev/resourcegraph/temp/defs.json");
        var definitions = defs.RootElement.GetProperty("defs");

        foreach (var definition in definitions.EnumerateObject())
        {
            Assert.NotEqual("union", definition.Value.GetProperty("type").GetString());
            Assert.NotEqual("ref", definition.Value.GetProperty("type").GetString());
        }

        var resource = definitions
            .GetProperty("membership")
            .GetProperty("properties")
            .GetProperty("resource");

        Assert.Equal("union", resource.GetProperty("type").GetString());
        Assert.Equal(3, resource.GetProperty("refs").GetArrayLength());
    }

    private static JsonDocument Parse(string relativePath) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, relativePath)));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ResourceGraph.sln")) &&
                Directory.Exists(Path.Combine(directory.FullName, "lexicons")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}

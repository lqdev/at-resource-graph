using System.Text.Json;
using ResourceGraph.Composition;
using ResourceGraph.Core;
using ResourceGraph.Discovery;
using ResourceGraph.Opml;
using ResourceGraph.Syndication;

return CliApplication.Run(args, Console.Out, Console.Error);

internal static class CliApplication
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
        {
            output.WriteLine(Usage);
            return 0;
        }

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "validate" => Validate(args, output, error),
                "parse-feed" => ParseFeed(args, output),
                "discover-head" => DiscoverHead(args, output),
                "import-opml" => ImportOpml(args, output, error),
                "export-opml" => ExportOpml(args, output, error),
                "expand" => Expand(args, output, error),
                _ => UnknownCommand(args[0], error)
            };
        }
        catch (FileNotFoundException exception)
        {
            error.WriteLine($"error: file not found: {exception.FileName ?? exception.Message}");
            return 2;
        }
        catch (DirectoryNotFoundException exception)
        {
            error.WriteLine($"error: directory not found: {exception.Message}");
            return 2;
        }
        catch (JsonException exception)
        {
            error.WriteLine($"error: invalid JSON: {exception.Message}");
            return 2;
        }
        catch (SyndicationParseException exception)
        {
            error.WriteLine($"error: feed parse failed: {exception.Message}");
            return 2;
        }
        catch (InvalidDataException exception)
        {
            error.WriteLine($"error: invalid input: {exception.Message}");
            return 2;
        }
        catch (ArgumentException exception)
        {
            error.WriteLine($"error: {exception.Message}");
            error.WriteLine(Usage);
            return 2;
        }
        catch (IOException exception)
        {
            error.WriteLine($"error: I/O failure: {exception.Message}");
            return 2;
        }
    }

    private static int Validate(string[] args, TextWriter output, TextWriter error)
    {
        var path = RequiredPath(args, "validate");
        var bundle = BundleJson.Deserialize(File.ReadAllText(path));
        var diagnostics = GraphValidator.Validate(bundle);
        output.WriteLine($"valid: {path}");
        WriteDiagnostics(diagnostics, error);
        return diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error) ? 1 : 0;
    }

    private static int ParseFeed(string[] args, TextWriter output)
    {
        var path = RequiredPath(args, "parse-feed");
        var feed = SyndicationParser.ParseFile(path);
        var result = new
        {
            format = feed.Format.ToString().ToLowerInvariant(),
            id = feed.Id,
            title = feed.Title,
            link = feed.Link?.AbsoluteUri,
            itemCount = feed.Items.Length,
            items = feed.Items.Select(item => new
            {
                id = item.Id,
                title = item.Title,
                link = item.Link?.AbsoluteUri,
                published = item.Published,
                enclosureCount = item.Enclosures.Length,
                metadata = item.Metadata
            })
        };
        output.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        return 0;
    }

    private static int DiscoverHead(string[] args, TextWriter output)
    {
        var path = RequiredPath(args, "discover-head");
        var result = HtmlHeadParser.ParseFile(path);
        output.WriteLine(JsonSerializer.Serialize(
            new
            {
                scannedBytes = result.ScannedBytes,
                reachedHeadEnd = result.ReachedHeadEnd,
                links = result.Links.Select(link => new
                {
                    kind = link.Kind.ToString(),
                    uri = link.UriText,
                    rel = link.Rel,
                    mediaType = link.MediaType,
                    title = link.Title
                }),
                diagnostics = result.Diagnostics
            },
            JsonOptions));
        return result.Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error) ? 1 : 0;
    }

    private static int ImportOpml(string[] args, TextWriter output, TextWriter error)
    {
        var path = RequiredPath(args, "import-opml");
        var profile = ReadProfile(args);
        var result = OpmlImporter.ImportFile(path, profile);
        if (result.Bundle is not null)
        {
            output.WriteLine(BundleJson.Serialize(result.Bundle));
        }

        WriteDiagnostics(result.Diagnostics, error);
        return result.IsSuccess ? 0 : 1;
    }

    private static int ExportOpml(string[] args, TextWriter output, TextWriter error)
    {
        var path = RequiredPath(args, "export-opml");
        var profile = ReadProfile(args);
        var outputPath = ReadOption(args, "--output");
        var bundle = BundleJson.Deserialize(File.ReadAllText(path));
        var result = OpmlExporter.Export(bundle, profile);
        if (outputPath is null)
        {
            output.WriteLine(result.Xml);
        }
        else
        {
            File.WriteAllText(outputPath, result.Xml);
            output.WriteLine($"wrote: {outputPath}");
        }

        WriteLossReport(result.LossReport, error);
        return 0;
    }

    private static int Expand(string[] args, TextWriter output, TextWriter error)
    {
        var path = RequiredPath(args, "expand");
        var bundle = BundleJson.Deserialize(File.ReadAllText(path));
        var result = new BundleExpander().ExpandAsync(
            bundle,
            new InMemoryBundleResolver(Array.Empty<Bundle>()))
            .AsTask()
            .GetAwaiter()
            .GetResult();

        output.WriteLine(JsonSerializer.Serialize(
            new
            {
                resources = result.Resources.Select(resource => new
                {
                    kind = resource.Reference.Kind.ToString(),
                    identity = resource.Reference.Identity,
                    provenance = resource.Provenance.Path.Select(step => new
                    {
                        bundle = step.BundleIdentity,
                        position = step.Position,
                        resource = step.ResourceIdentity
                    })
                }),
                diagnostics = result.Diagnostics
            },
            JsonOptions));
        WriteDiagnostics(result.Diagnostics, error);
        return result.IsComplete ? 0 : 1;
    }

    private static OpmlProfile ReadProfile(string[] args)
    {
        var value = ReadOption(args, "--profile") ?? "conventional";
        return value.ToLowerInvariant() switch
        {
            "conventional" => OpmlProfile.Conventional,
            "graph" => OpmlProfile.Graph,
            _ => throw new ArgumentException("Profile must be 'conventional' or 'graph'.", nameof(args))
        };
    }

    private static string RequiredPath(string[] args, string command)
    {
        if (args.Length < 2 || args[1].StartsWith('-'))
        {
            throw new ArgumentException($"The {command} command requires an input path.", nameof(args));
        }

        return args[1];
    }

    private static string? ReadOption(string[] args, string name)
    {
        for (var index = 1; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    private static int UnknownCommand(string command, TextWriter error)
    {
        error.WriteLine($"error: unknown command '{command}'.");
        error.WriteLine(Usage);
        return 2;
    }

    private static void WriteDiagnostics(IEnumerable<GraphDiagnostic> diagnostics, TextWriter error)
    {
        foreach (var diagnostic in diagnostics)
        {
            error.WriteLine(
                $"[{diagnostic.Severity}] {diagnostic.Code}: {diagnostic.Message}");
        }
    }

    private static void WriteLossReport(OpmlLossReport report, TextWriter error)
    {
        foreach (var note in report.Notes)
        {
            error.WriteLine($"[loss] {note}");
        }
    }

    private const string Usage = """
        ResourceGraph CLI (offline bootstrap)

        Commands:
          validate <bundle.json>
          parse-feed <feed.xml>
          discover-head <page.html>
          import-opml <file.opml> [--profile conventional|graph]
          export-opml <bundle.json> [--profile conventional|graph] [--output file.opml]
          expand <bundle.json>
        """;
}

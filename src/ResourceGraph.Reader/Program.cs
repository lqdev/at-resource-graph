using System.Text.Json;
using ResourceGraph.Core;
using ResourceGraph.Reader;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseKestrel(options => options.AddServerHeader = false);
var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'none'; style-src 'unsafe-inline'; connect-src 'self';";
    await next();
});

app.MapGet("/", () => Results.Content(
    """
    <!doctype html>
    <html lang="en">
      <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>AT Resource Graph Reader</title>
        <style>
          body { font-family: system-ui, sans-serif; margin: 2rem; max-width: 60rem; }
          code { background: #eee; padding: .1rem .25rem; }
        </style>
      </head>
      <body>
        <h1>AT Resource Graph Reader</h1>
        <p>This offline reference reader exposes a sample bundle and parsed feed.</p>
        <p>Use <code>/api/bundle</code> and <code>/api/feed</code>. No arbitrary URL fetching is enabled.</p>
      </body>
    </html>
    """,
    "text/html; charset=utf-8"));

app.MapGet("/api/bundle", () =>
    Results.Content(BundleJson.Serialize(SampleReaderData.Bundle), "application/json; charset=utf-8"));

app.MapGet("/api/feed", () =>
    Results.Json(new
    {
        format = SampleReaderData.Feed.Format.ToString().ToLowerInvariant(),
        title = SampleReaderData.Feed.Title,
        link = SampleReaderData.Feed.Link?.AbsoluteUri,
        items = SampleReaderData.Feed.Items.Select(item => new
        {
            id = item.Id,
            title = item.Title,
            link = item.Link?.AbsoluteUri,
            summary = item.Summary,
            published = item.Published
        })
    }));

app.MapPost("/api/validate", async (HttpRequest request) =>
{
    try
    {
        var json = await ReadBoundedBodyAsync(request, 64 * 1024);
        var bundle = BundleJson.Deserialize(json);
        return Results.Json(new
        {
            valid = true,
            name = bundle.Name,
            memberCount = bundle.Members.Length,
            diagnostics = GraphValidator.Validate(bundle)
        });
    }
    catch (InvalidDataException exception)
    {
        return Results.Json(
            new { valid = false, error = exception.Message },
            statusCode: StatusCodes.Status413RequestEntityTooLarge);
    }
    catch (JsonException exception)
    {
        return Results.Json(
            new { valid = false, error = exception.Message },
            statusCode: StatusCodes.Status400BadRequest);
    }
    catch (ArgumentException exception)
    {
        return Results.Json(
            new { valid = false, error = exception.Message },
            statusCode: StatusCodes.Status400BadRequest);
    }
});

app.Run();

static async Task<string> ReadBoundedBodyAsync(HttpRequest request, int maximumBytes)
{
    if (request.ContentLength > maximumBytes)
    {
        throw new InvalidDataException("The request body exceeds the 64 KiB limit.");
    }

    using var buffer = new MemoryStream();
    var chunk = new byte[8192];
    var total = 0;
    int read;
    while ((read = await request.Body.ReadAsync(chunk)) > 0)
    {
        total += read;
        if (total > maximumBytes)
        {
            throw new InvalidDataException("The request body exceeds the 64 KiB limit.");
        }

        await buffer.WriteAsync(chunk.AsMemory(0, read));
    }

    return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
}

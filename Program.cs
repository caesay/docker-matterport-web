using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var log = app.Logger;

var _matterportKey = Environment.GetEnvironmentVariable("DMW_KEY");
var _matterportPath = Environment.GetEnvironmentVariable("DMW_PATH");
var _serverBaseUrl = Environment.GetEnvironmentVariable("DMW_BASEURL");

log.LogInformation("Initialising Docker Matterport Web");
log.LogInformation("MatterportKey = {}", _matterportKey);
log.LogInformation("MatterportPath = {}", _matterportPath);

if (!Directory.Exists(_matterportPath))
{
    log.LogError("Invalid Matterport path: {}", _matterportPath);
    return;
}

if (String.IsNullOrWhiteSpace(_matterportKey))
{
    log.LogError("Invalid Matterport key: {}", _matterportKey);
    return;
}

app.Map("/api/mp/models/graph", ([FromQuery] string operation, [FromQuery] string operationName, HttpContext context) =>
{
    operation ??= operationName;

    var defaultFilePath = Path.Combine(_matterportPath, $"api/mp/models/graph");
    if (String.IsNullOrWhiteSpace(operation))
    {
        return Results.File(defaultFilePath, "application/json");
    }

    var baseUrl = _serverBaseUrl ?? $"{context.Request.Scheme}://{context.Request.Host}";
    var modifiedFilePath = Path.Combine(_matterportPath, $"api/mp/models/graph_{operation}.modified.json");
    if (File.Exists(modifiedFilePath))
    {
        return FilteredFileResult(modifiedFilePath, "application/json", baseUrl);
    }

    var filePath = Path.Combine(_matterportPath, $"api/mp/models/graph_{operation}.json");
    if (File.Exists(filePath))
    {
        return FilteredFileResult(filePath, "application/json", baseUrl);
    }

    log.LogWarning("Invalid graph request: {}", operation);
    return Results.BadRequest();
});

app.MapGet("/", () =>
{
    var filePath = Path.Combine(_matterportPath, $"index.modified.html");
    return Results.File(filePath, "text/html");
});

app.Map("/api/v1/event", () => Results.Ok());

app.MapGet("/{**catchAll}", (string catchAll) =>
{
    var filePath = Path.Combine(_matterportPath, catchAll);
    if (filePath.EndsWith(".js") || filePath.EndsWith(".json") || filePath.EndsWith(".html"))
    {
        var modifiedFilePath = Path.ChangeExtension(filePath, ".modified" + Path.GetExtension(filePath));

        if (File.Exists(modifiedFilePath))
        {
            log.LogInformation("Serving modified file: {}", modifiedFilePath);
            return Results.File(filePath, GetMimeType(modifiedFilePath));
        }
    }

    if (File.Exists(filePath))
    {
        return Results.File(filePath, GetMimeType(filePath));
    }

    log.LogWarning("404: {}", filePath);
    return Results.NotFound();
});

app.Run();

static string GetMimeType(string fileName)
{
    var provider = new FileExtensionContentTypeProvider();
    if (!provider.TryGetContentType(fileName, out var contentType))
    {
        contentType = "application/octet-stream";
    }

    return contentType;
}

static IResult FilteredFileResult(string filePath, string contentType, string baseUrl)
{
    var fileContents = File.ReadAllText(filePath);
    fileContents = fileContents.Replace("https://cdn-2.matterport.com", baseUrl);
    return Results.Content(fileContents, contentType);
}
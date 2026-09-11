using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Events;
using CadpIntegration.Services;
using CadpIntegration.Models;
using System;

var builder = WebApplication.CreateBuilder(args);

// Read LogLevel from env mapping, default to Information
var logLevelEnv = Environment.GetEnvironmentVariable("LOG_LEVEL") ?? "Information";
if (!Enum.TryParse(logLevelEnv, true, out LogEventLevel minimumLevel))
{
    minimumLevel = LogEventLevel.Information;
}

// 1. Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Is(minimumLevel)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{Component}] [{Operation}] [CorrelationId: {CorrelationId}] Result: {Result} Duration: {Duration}ms - {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/log.txt",
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{Component}] [{Operation}] [CorrelationId: {CorrelationId}] Result: {Result} Duration: {Duration}ms - {Message:lj}{NewLine}{Exception}",
        rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// 2. Map Configuration & Register Services
builder.Services.Configure<CadpSettings>(builder.Configuration);
builder.Services.AddRazorPages();
builder.Services.AddSingleton<IEncryptionService, CadpEncryptionService>();
builder.Services.AddHttpClient<IKmipService, KmipService>();
builder.Services.AddSingleton<INaeXmlClient, NaeXmlClient>();
builder.Services.AddSingleton<ITestRunnerService, TestRunnerService>();

// File upload size limit (50MB)
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 50 * 1024 * 1024);

var app = builder.Build();

// Correlation ID Middleware
app.Use(async (context, next) =>
{
    var correlationId = Guid.NewGuid().ToString();
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
    using (Serilog.Context.LogContext.PushProperty("Component", "WebShell"))
    using (Serilog.Context.LogContext.PushProperty("Operation", context.Request.Path))
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Serilog.Context.LogContext.PushProperty("Result", "InProgress");
        try
        {
            await next(context);
            watch.Stop();
            Log.Information("Request completed in {ElapsedMs}ms", watch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            watch.Stop();
            Log.Error(ex, "Request failed after {ElapsedMs}ms", watch.ElapsedMilliseconds);
            throw;
        }
    }
});

app.MapRazorPages();

// ═══════════════════════════════════════════════════════════
// Health Endpoint
// ═══════════════════════════════════════════════════════════
app.MapGet("/health", (IConfiguration config) =>
{
    var settings = new CadpSettings();
    config.Bind(settings);
    return Results.Json(new
    {
        application = "UP",
        kmip = string.IsNullOrEmpty(settings.KmipHost) ? "NOT CONFIGURED" : "DOWN",
        naeXml = string.IsNullOrEmpty(settings.NaeHost) ? "NOT CONFIGURED" : "DOWN",
        cadp = string.IsNullOrEmpty(settings.CadpHost) ? "NOT CONFIGURED" : "DOWN"
    });
});

// ═══════════════════════════════════════════════════════════
// Phase 4 — String Encryption API
// ═══════════════════════════════════════════════════════════
app.MapPost("/api/encryption/string/encrypt", async (StringEncryptRequest req, IEncryptionService svc, CancellationToken ct) =>
{
    var result = await svc.EncryptStringAsync(req.Plaintext, req.KeyId, req.Algorithm, ct);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

app.MapPost("/api/encryption/string/decrypt", async (StringDecryptRequest req, IEncryptionService svc, CancellationToken ct) =>
{
    var result = await svc.DecryptStringAsync(req.Ciphertext, req.KeyId, req.Algorithm, ct);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

// ═══════════════════════════════════════════════════════════
// Phase 5 — File Encryption API
// ═══════════════════════════════════════════════════════════
app.MapPost("/api/encryption/file/encrypt", async (HttpContext ctx, IEncryptionService svc, CancellationToken ct) =>
{
    var form = await ctx.Request.ReadFormAsync(ct);
    var file = form.Files.GetFile("file");
    var keyId = form["keyId"].ToString();
    var algorithm = form["algorithm"].ToString();

    if (file == null || file.Length == 0)
        return Results.BadRequest(new FileEncryptionResult { Success = false, Error = "No file uploaded." });
    if (string.IsNullOrWhiteSpace(keyId))
        return Results.BadRequest(new FileEncryptionResult { Success = false, Error = "Key ID is required." });

    var outputPath = Path.Combine(Path.GetTempPath(), $"enc_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}");
    using var inputStream = file.OpenReadStream();
    using var outputStream = File.Create(outputPath);

    var result = await svc.EncryptFileAsync(inputStream, outputStream, keyId, algorithm, ct);
    if (!result.Success)
    {
        try { File.Delete(outputPath); } catch { }
        return Results.BadRequest(result);
    }

    result.BytesProcessed = file.Length;
    result.OutputFileName = Path.GetFileName(outputPath);
    return Results.Ok(result);
});

app.MapPost("/api/encryption/file/decrypt", async (HttpContext ctx, IEncryptionService svc, CancellationToken ct) =>
{
    var form = await ctx.Request.ReadFormAsync(ct);
    var file = form.Files.GetFile("file");
    var keyId = form["keyId"].ToString();
    var algorithm = form["algorithm"].ToString();

    if (file == null || file.Length == 0)
        return Results.BadRequest(new FileEncryptionResult { Success = false, Error = "No file uploaded." });
    if (string.IsNullOrWhiteSpace(keyId))
        return Results.BadRequest(new FileEncryptionResult { Success = false, Error = "Key ID is required." });

    var outputPath = Path.Combine(Path.GetTempPath(), $"dec_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}");
    using var inputStream = file.OpenReadStream();
    using var outputStream = File.Create(outputPath);

    var result = await svc.DecryptFileAsync(inputStream, outputStream, keyId, algorithm, ct);
    if (!result.Success)
    {
        try { File.Delete(outputPath); } catch { }
        return Results.BadRequest(result);
    }

    result.BytesProcessed = file.Length;
    result.OutputFileName = Path.GetFileName(outputPath);
    return Results.Ok(result);
});

// ═══════════════════════════════════════════════════════════
// Phase 6 — KMIP API
// ═══════════════════════════════════════════════════════════
app.MapPost("/api/kmip/create", async (KmipCreateRequest req, IKmipService svc, CancellationToken ct) =>
{
    var result = await svc.CreateKeyAsync(req.Name, req.Algorithm, req.KeySize, ct);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

app.MapPost("/api/kmip/locate", async (KmipLocateRequest req, IKmipService svc, CancellationToken ct) =>
{
    var result = await svc.LocateKeysAsync(req.Name, req.Algorithm, req.State, ct);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

app.MapPost("/api/kmip/get", async (KmipUuidRequest req, IKmipService svc, CancellationToken ct) =>
{
    var result = await svc.GetKeyAsync(req.Uuid, ct);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

app.MapPost("/api/kmip/activate", async (KmipUuidRequest req, IKmipService svc, CancellationToken ct) =>
{
    var result = await svc.ActivateKeyAsync(req.Uuid, ct);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

app.MapPost("/api/kmip/revoke", async (KmipUuidRequest req, IKmipService svc, CancellationToken ct) =>
{
    var result = await svc.RevokeKeyAsync(req.Uuid, ct);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

// ═══════════════════════════════════════════════════════════
// Phase 7 — NAE-XML API
// ═══════════════════════════════════════════════════════════
app.MapPost("/api/nae/connect", async (INaeXmlClient nae, CancellationToken ct) =>
{
    var result = await nae.ConnectAsync(ct);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

app.MapPost("/api/nae/request", async (NaeRequestPayload req, INaeXmlClient nae, CancellationToken ct) =>
{
    // TODO: Build proper NAE-XML request from Thales docs
    var xmlRequest = $"<NaeRequest><Operation>{req.Operation}</Operation><KeyName>{req.KeyName}</KeyName><Data>{req.Data}</Data></NaeRequest>";
    var result = await nae.SendRequestAsync(xmlRequest, ct);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

// ═══════════════════════════════════════════════════════════
// Phase 8 & 9 — Test Runner API
// ═══════════════════════════════════════════════════════════
app.MapPost("/api/tests/run-positive", async (ITestRunnerService svc) =>
{
    return Results.Ok(await svc.RunPositiveTestsAsync());
});

app.MapPost("/api/tests/run-negative", async (ITestRunnerService svc) =>
{
    return Results.Ok(await svc.RunNegativeTestsAsync());
});

// ═══════════════════════════════════════════════════════════
// Phase 12 — Logs & Config API
// ═══════════════════════════════════════════════════════════
app.MapGet("/api/config", (IConfiguration config) =>
{
    var settings = new CadpSettings();
    config.Bind(settings);

    string Mask(string val) => string.IsNullOrEmpty(val) ? "Not Set" : "***";

    return Results.Json(new
    {
        CADP = new { Host = settings.CadpHost, Port = settings.CadpPort },
        KMIP = new { Host = settings.KmipHost, Port = settings.KmipPort, Credentials = Mask("dummy") },
        NAE_XML = new { Host = settings.NaeHost, Port = settings.NaePort, Credentials = Mask("dummy") }
    });
});

app.MapGet("/api/logs", () =>
{
    var logFiles = Directory.GetFiles("logs", "log*.txt");
    if (!logFiles.Any()) return Results.Ok(new List<string>());
    
    var lastFile = logFiles.OrderByDescending(f => f).First();
    try 
    {
        var lines = System.IO.File.ReadAllLines(lastFile);
        return Results.Ok(lines);
    }
    catch 
    {
        return Results.Ok(new List<string> { "Unable to read logs." });
    }
});

app.Run();

// Strongly typed settings
public class CadpSettings
{
    public string CadpHost { get; set; } = "";
    public int CadpPort { get; set; }
    public string KmipHost { get; set; } = "";
    public int KmipPort { get; set; } = 5696;
    public string NaeHost { get; set; } = "";
    public int NaePort { get; set; } = 9000;
}

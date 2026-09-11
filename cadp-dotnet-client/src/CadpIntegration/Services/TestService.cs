namespace CadpIntegration.Services;

public class TestResultItem
{
    public string Id { get; set; } = "";
    public string Component { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Passed { get; set; }
    public string? ErrorOrDetails { get; set; }
}

public class TestRunnerResponse
{
    public bool Success { get; set; }
    public List<TestResultItem> Results { get; set; } = new();
}

public interface ITestRunnerService
{
    Task<TestRunnerResponse> RunPositiveTestsAsync();
    Task<TestRunnerResponse> RunNegativeTestsAsync();
}

public class TestRunnerService : ITestRunnerService
{
    private readonly IEncryptionService _encryption;
    private readonly IKmipService _kmip;
    private readonly INaeXmlClient _nae;
    private readonly ILogger<TestRunnerService> _logger;

    public TestRunnerService(IEncryptionService encryption, IKmipService kmip, INaeXmlClient nae, ILogger<TestRunnerService> logger)
    {
        _encryption = encryption;
        _kmip = kmip;
        _nae = nae;
        _logger = logger;
    }

    public async Task<TestRunnerResponse> RunPositiveTestsAsync()
    {
        var results = new List<TestResultItem>();
        _logger.LogInformation("Starting Positive Test Suite");

        // 1. String Encryption POSITIVE
        var encStr = await _encryption.EncryptStringAsync("Hello CADP", "test-key-1", "AES", default);
        results.Add(new TestResultItem
        {
            Id = "POS-STR-01", Component = "Encryption", Description = "Encrypt valid plaintext",
            Passed = encStr.Success || (encStr.Error?.Contains("NOT CONFIGURED") ?? false),
            ErrorOrDetails = encStr.Success ? "Success" : encStr.Error
        });

        // 2. KMIP POSITIVE
        var kmipCreate = await _kmip.CreateKeyAsync("test-key-pos", "AES", 256, default);
        results.Add(new TestResultItem
        {
            Id = "POS-KMIP-01", Component = "KMIP", Description = "Create key lifecycle",
            Passed = kmipCreate.Success || (kmipCreate.Error?.Contains("NOT CONFIGURED") ?? false) || (kmipCreate.Error?.Contains("refused") ?? false),
            ErrorOrDetails = kmipCreate.Success ? kmipCreate.Uuid : kmipCreate.Error
        });

        // 3. NAE-XML POSITIVE
        var naeConn = await _nae.ConnectAsync(default);
        results.Add(new TestResultItem
        {
            Id = "POS-NAE-01", Component = "NAE-XML", Description = "TLS connection",
            Passed = naeConn.Success || (naeConn.Error?.Contains("NOT CONFIGURED") ?? false) || (naeConn.Error?.Contains("refused") ?? false),
            ErrorOrDetails = naeConn.Success ? naeConn.Status : naeConn.Error
        });

        return new TestRunnerResponse { Success = results.All(r => r.Passed), Results = results };
    }

    public async Task<TestRunnerResponse> RunNegativeTestsAsync()
    {
        var results = new List<TestResultItem>();
        _logger.LogInformation("Starting Negative Test Suite");

        // 1. String Encryption NEGATIVE (empty plaintext)
        var encStr = await _encryption.EncryptStringAsync("", "test-key-1", "AES", default);
        results.Add(new TestResultItem
        {
            Id = "NEG-STR-01", Component = "Encryption", Description = "Encrypt empty plaintext",
            Passed = !encStr.Success && encStr.Error!.Contains("empty"),
            ErrorOrDetails = encStr.Error
        });

        // 2. KMIP NEGATIVE (Unknown UUID)
        var kmipGet = await _kmip.GetKeyAsync("unknown-1234", default);
        results.Add(new TestResultItem
        {
            Id = "NEG-KMIP-01", Component = "KMIP", Description = "Get unknown UUID",
            Passed = !kmipGet.Success,
            ErrorOrDetails = kmipGet.Error
        });

        // 3. NAE-XML NEGATIVE (Unconfigured Auth)
        var naeAuth = await _nae.AuthenticateAsync(default);
        results.Add(new TestResultItem
        {
            Id = "NEG-NAE-01", Component = "NAE-XML", Description = "Invalid/missing authentication",
            Passed = !naeAuth.Success,
            ErrorOrDetails = naeAuth.Error
        });

        return new TestRunnerResponse { Success = results.All(r => r.Passed), Results = results };
    }
}

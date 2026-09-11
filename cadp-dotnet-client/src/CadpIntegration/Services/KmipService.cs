using CadpIntegration.Models;

namespace CadpIntegration.Services;

/// <summary>
/// Interface for KMIP key lifecycle operations.
/// NO hard-delete. NO destroy.
/// </summary>
public interface IKmipService
{
    Task<KmipResult> CreateKeyAsync(string name, string algorithm, int keySize, CancellationToken ct);
    Task<KmipLocateResult> LocateKeysAsync(string? name, string? algorithm, string? state, CancellationToken ct);
    Task<KmipResult> GetKeyAsync(string uuid, CancellationToken ct);
    Task<KmipResult> ActivateKeyAsync(string uuid, CancellationToken ct);
    Task<KmipResult> RevokeKeyAsync(string uuid, CancellationToken ct);
}

/// <summary>
/// KMIP Service that proxies to the Python PyKMIP microservice.
/// Calls REST endpoints on the kmip-client container.
/// </summary>
public class KmipService : IKmipService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<KmipService> _logger;
    private readonly CadpSettings _settings;

    public KmipService(HttpClient httpClient, ILogger<KmipService> logger, Microsoft.Extensions.Options.IOptions<CadpSettings> settings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings.Value;
    }

    private bool IsConfigured => !string.IsNullOrEmpty(_settings.KmipHost);

    public async Task<KmipResult> CreateKeyAsync(string name, string algorithm, int keySize, CancellationToken ct)
    {
        if (!IsConfigured)
            return new KmipResult { Success = false, Error = "KMIP: NOT CONFIGURED. Set KMIP_HOST environment variable." };

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"http://kmip-client:5000/kmip/create",
                new { name, algorithm, key_size = keySize }, ct);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<KmipResult>(ct);
                return result ?? new KmipResult { Success = false, Error = "Empty response from KMIP service." };
            }
            return new KmipResult { Success = false, Error = $"KMIP service returned {response.StatusCode}" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "KMIP create key failed");
            return new KmipResult { Success = false, Error = $"KMIP connection failed: {ex.Message}" };
        }
    }

    public async Task<KmipLocateResult> LocateKeysAsync(string? name, string? algorithm, string? state, CancellationToken ct)
    {
        if (!IsConfigured)
            return new KmipLocateResult { Success = false, Error = "KMIP: NOT CONFIGURED." };

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"http://kmip-client:5000/kmip/locate",
                new { name, algorithm, state }, ct);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<KmipLocateResult>(ct);
                return result ?? new KmipLocateResult { Success = false, Error = "Empty response." };
            }
            return new KmipLocateResult { Success = false, Error = $"KMIP service returned {response.StatusCode}" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "KMIP locate failed");
            return new KmipLocateResult { Success = false, Error = $"KMIP connection failed: {ex.Message}" };
        }
    }

    public async Task<KmipResult> GetKeyAsync(string uuid, CancellationToken ct)
    {
        if (!IsConfigured)
            return new KmipResult { Success = false, Error = "KMIP: NOT CONFIGURED." };

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"http://kmip-client:5000/kmip/get",
                new { uuid }, ct);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<KmipResult>(ct);
                return result ?? new KmipResult { Success = false, Error = "Empty response." };
            }
            return new KmipResult { Success = false, Error = $"KMIP service returned {response.StatusCode}" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "KMIP get key failed");
            return new KmipResult { Success = false, Error = $"KMIP connection failed: {ex.Message}" };
        }
    }

    public async Task<KmipResult> ActivateKeyAsync(string uuid, CancellationToken ct)
    {
        if (!IsConfigured)
            return new KmipResult { Success = false, Error = "KMIP: NOT CONFIGURED." };

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"http://kmip-client:5000/kmip/activate",
                new { uuid }, ct);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<KmipResult>(ct);
                return result ?? new KmipResult { Success = false, Error = "Empty response." };
            }
            return new KmipResult { Success = false, Error = $"KMIP service returned {response.StatusCode}" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "KMIP activate failed");
            return new KmipResult { Success = false, Error = $"KMIP connection failed: {ex.Message}" };
        }
    }

    public async Task<KmipResult> RevokeKeyAsync(string uuid, CancellationToken ct)
    {
        if (!IsConfigured)
            return new KmipResult { Success = false, Error = "KMIP: NOT CONFIGURED." };

        try
        {
            // NO destroy/delete — only revoke
            var response = await _httpClient.PostAsJsonAsync($"http://kmip-client:5000/kmip/revoke",
                new { uuid }, ct);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<KmipResult>(ct);
                return result ?? new KmipResult { Success = false, Error = "Empty response." };
            }
            return new KmipResult { Success = false, Error = $"KMIP service returned {response.StatusCode}" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "KMIP revoke failed");
            return new KmipResult { Success = false, Error = $"KMIP connection failed: {ex.Message}" };
        }
    }
}

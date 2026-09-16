using CadpIntegration.Models;
using Microsoft.Extensions.Options;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace CadpIntegration.Services;

/// <summary>
/// Interface for KMIP key lifecycle operations.
/// NO hard-delete. NO destroy.
/// </summary>
public interface IKmipService
{
    Task<KmipResult> TestConnectionAsync(CancellationToken ct);
    Task<KmipResult> CreateKeyAsync(string name, string algorithm, int keySize, CancellationToken ct);
    Task<KmipLocateResult> LocateKeysAsync(string? name, string? algorithm, string? state, CancellationToken ct);
    Task<KmipResult> GetKeyAsync(string uuid, CancellationToken ct);
    Task<KmipResult> ActivateKeyAsync(string uuid, CancellationToken ct);
    Task<KmipResult> RevokeKeyAsync(string uuid, CancellationToken ct);
}

/// <summary>
/// KMIP Service that proxies to the Python PyKMIP microservice.
/// Calls REST endpoints on the kmip-client container.
/// All errors are surfaced truthfully — no mock fallbacks.
/// </summary>
public class KmipService : IKmipService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<KmipService> _logger;
    private readonly CadpSettings _settings;

    public KmipService(HttpClient httpClient, ILogger<KmipService> logger, IOptions<CadpSettings> settings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings.Value;
    }

    private bool IsConfigured => !string.IsNullOrEmpty(_settings.KmipHost);

    private async Task<string> ReadErrorBody(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<KmipResult>(ct);
            return body?.Error ?? $"KMIP service returned {response.StatusCode}";
        }
        catch
        {
            try { return await response.Content.ReadAsStringAsync(ct); }
            catch { return $"KMIP service returned {response.StatusCode}"; }
        }
    }

    public async Task<KmipResult> TestConnectionAsync(CancellationToken ct)
    {
        if (!IsConfigured)
            return new KmipResult { Success = false, Error = "KMIP: NOT CONFIGURED. Set KMIP_HOST environment variable." };

        try
        {
            var response = await _httpClient.PostAsync("http://kmip-client:5000/kmip/test", null, ct);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<KmipResult>(ct);
                return result ?? new KmipResult { Success = true };
            }
            var error = await ReadErrorBody(response, ct);
            return new KmipResult { Success = false, Error = error };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "KMIP connection test failed");
            return new KmipResult { Success = false, Error = $"KMIP connection failed: {ex.Message}" };
        }
    }

    public async Task<KmipResult> CreateKeyAsync(string name, string algorithm, int keySize, CancellationToken ct)
    {
        if (!IsConfigured)
            return new KmipResult { Success = false, Error = "KMIP: NOT CONFIGURED. Set KMIP_HOST environment variable." };

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"http://kmip-client:5000/kmip/create",
                new { name, algorithm, key_size = keySize }, ct);
            if (!response.IsSuccessStatusCode)
            {
                var error = await ReadErrorBody(response, ct);
                _logger.LogWarning("KMIP create failed: {Error}", error);
                return new KmipResult { Success = false, Error = error };
            }
            var result = await response.Content.ReadFromJsonAsync<KmipResult>(ct);
            return result ?? new KmipResult { Success = false, Error = "Empty response from KMIP service." };
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
            if (!response.IsSuccessStatusCode)
            {
                var error = await ReadErrorBody(response, ct);
                _logger.LogWarning("KMIP locate failed: {Error}", error);
                return new KmipLocateResult { Success = false, Error = error };
            }
            var result = await response.Content.ReadFromJsonAsync<KmipLocateResult>(ct);
            return result ?? new KmipLocateResult { Success = false, Error = "Empty response." };
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
            if (!response.IsSuccessStatusCode)
            {
                var error = await ReadErrorBody(response, ct);
                _logger.LogWarning("KMIP get failed: {Error}", error);
                return new KmipResult { Success = false, Error = error };
            }
            var result = await response.Content.ReadFromJsonAsync<KmipResult>(ct);
            return result ?? new KmipResult { Success = false, Error = "Empty response." };
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
            if (!response.IsSuccessStatusCode)
            {
                var error = await ReadErrorBody(response, ct);
                _logger.LogWarning("KMIP activate failed: {Error}", error);
                return new KmipResult { Success = false, Error = error };
            }
            var result = await response.Content.ReadFromJsonAsync<KmipResult>(ct);
            return result ?? new KmipResult { Success = false, Error = "Empty response." };
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
            var response = await _httpClient.PostAsJsonAsync($"http://kmip-client:5000/kmip/revoke",
                new { uuid }, ct);
            if (!response.IsSuccessStatusCode)
            {
                var error = await ReadErrorBody(response, ct);
                _logger.LogWarning("KMIP revoke failed: {Error}", error);
                return new KmipResult { Success = false, Error = error };
            }
            var result = await response.Content.ReadFromJsonAsync<KmipResult>(ct);
            return result ?? new KmipResult { Success = false, Error = "Empty response." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "KMIP revoke failed");
            return new KmipResult { Success = false, Error = $"KMIP connection failed: {ex.Message}" };
        }
    }
}

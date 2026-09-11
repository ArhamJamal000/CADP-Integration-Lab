using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using CadpIntegration.Models;

namespace CadpIntegration.Services;

/// <summary>
/// Interface for NAE-XML TLS client operations.
/// Do NOT invent XML protocol — use stubs with TODO markers until Thales docs are available.
/// </summary>
public interface INaeXmlClient
{
    Task<NaeResponse> ConnectAsync(CancellationToken ct);
    Task<NaeResponse> AuthenticateAsync(CancellationToken ct);
    Task<NaeResponse> SendRequestAsync(string xmlRequest, CancellationToken ct);
    NaeResponse ParseResponse(string rawResponse);
    Task CloseAsync();
}

/// <summary>
/// NAE-XML TLS Client adapter.
/// TODO: Wire to actual NAE-XML protocol when Thales documentation is available.
/// Never expose passwords, private keys, or raw secrets in logs or UI.
/// </summary>
public class NaeXmlClient : INaeXmlClient
{
    private readonly ILogger<NaeXmlClient> _logger;
    private readonly CadpSettings _settings;
    private TcpClient? _tcpClient;
    private SslStream? _sslStream;

    public NaeXmlClient(ILogger<NaeXmlClient> logger, Microsoft.Extensions.Options.IOptions<CadpSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
    }

    private bool IsConfigured => !string.IsNullOrEmpty(_settings.NaeHost);

    public async Task<NaeResponse> ConnectAsync(CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return new NaeResponse
            {
                Success = false,
                Operation = "Connect",
                Error = "NAE-XML: NOT CONFIGURED. Set NAE_HOST and NAE_PORT environment variables.",
                Status = "NOT_CONFIGURED"
            };
        }

        try
        {
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(_settings.NaeHost, _settings.NaePort, ct);

            _sslStream = new SslStream(
                _tcpClient.GetStream(),
                false,
                ValidateServerCertificate,
                null);

            // TODO: Load client certificates from TLS_CLIENT_CERT_PATH and TLS_CLIENT_KEY_PATH
            await _sslStream.AuthenticateAsClientAsync(_settings.NaeHost);

            _logger.LogInformation("NAE-XML TLS connection established to {Host}:{Port}", _settings.NaeHost, _settings.NaePort);

            return new NaeResponse
            {
                Success = true,
                Operation = "Connect",
                Status = "CONNECTED",
                ResponseBody = $"TLS connection established to {_settings.NaeHost}:{_settings.NaePort}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NAE-XML TLS connection failed");
            return new NaeResponse
            {
                Success = false,
                Operation = "Connect",
                Error = $"NAE-XML connection failed: {ex.Message}",
                Status = "CONNECTION_FAILED"
            };
        }
    }

    public Task<NaeResponse> AuthenticateAsync(CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return Task.FromResult(new NaeResponse
            {
                Success = false,
                Operation = "Authenticate",
                Error = "NAE-XML: NOT CONFIGURED.",
                Status = "NOT_CONFIGURED"
            });
        }

        // TODO: Wire to actual NAE-XML authentication protocol
        // Do NOT log NAE_USERNAME or NAE_PASSWORD
        _logger.LogWarning("NAE-XML authentication stub — wire to actual protocol.");
        return Task.FromResult(new NaeResponse
        {
            Success = false,
            Operation = "Authenticate",
            Error = "NAE-XML: Authentication protocol not yet implemented. Wire to actual Thales NAE-XML docs.",
            Status = "NOT_IMPLEMENTED"
        });
    }

    public async Task<NaeResponse> SendRequestAsync(string xmlRequest, CancellationToken ct)
    {
        if (_sslStream == null || !(_tcpClient?.Connected ?? false))
        {
            return new NaeResponse
            {
                Success = false,
                Operation = "SendRequest",
                Error = "NAE-XML: Not connected. Call Connect first.",
                Status = "NOT_CONNECTED"
            };
        }

        try
        {
            // TODO: Wire to actual NAE-XML protocol format from Thales docs
            // Currently sends raw XML as-is
            var requestBytes = Encoding.UTF8.GetBytes(xmlRequest);
            await _sslStream.WriteAsync(requestBytes, ct);
            await _sslStream.FlushAsync(ct);

            var responseBuffer = new byte[4096];
            int bytesRead = await _sslStream.ReadAsync(responseBuffer, ct);
            var rawResponse = Encoding.UTF8.GetString(responseBuffer, 0, bytesRead);

            return ParseResponse(rawResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NAE-XML request failed");
            return new NaeResponse
            {
                Success = false,
                Operation = "SendRequest",
                Error = $"NAE-XML request failed: {ex.Message}",
                Status = "REQUEST_FAILED"
            };
        }
    }

    public NaeResponse ParseResponse(string rawResponse)
    {
        // TODO: Parse actual NAE-XML response structure from Thales docs
        return new NaeResponse
        {
            Success = !string.IsNullOrEmpty(rawResponse),
            Operation = "ParseResponse",
            ResponseBody = rawResponse,
            Status = string.IsNullOrEmpty(rawResponse) ? "EMPTY_RESPONSE" : "OK"
        };
    }

    public async Task CloseAsync()
    {
        if (_sslStream != null)
        {
            await _sslStream.DisposeAsync();
            _sslStream = null;
        }
        _tcpClient?.Dispose();
        _tcpClient = null;
        _logger.LogInformation("NAE-XML connection closed.");
    }

    private bool ValidateServerCertificate(object sender, X509Certificate? certificate,
        X509Chain? chain, SslPolicyErrors sslPolicyErrors)
    {
        // Do NOT trust-all by default
        // TODO: Load CA cert from TLS_CA_CERT_PATH for validation
        if (sslPolicyErrors == SslPolicyErrors.None)
            return true;

        _logger.LogWarning("NAE-XML TLS certificate validation error: {Errors}", sslPolicyErrors);
        return false;
    }
}

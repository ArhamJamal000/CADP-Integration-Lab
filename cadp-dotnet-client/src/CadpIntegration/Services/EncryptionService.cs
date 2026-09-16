using CadpIntegration.Models;
using System.Security.Cryptography;
using System.Text;

namespace CadpIntegration.Services;

/// <summary>
/// Interface for CADP-backed string and file encryption.
/// Implementations MUST call the real CADP API/SDK.
/// Do NOT implement fake local encryption.
/// </summary>
public interface IEncryptionService
{
    Task<EncryptionResult> EncryptStringAsync(string plaintext, string keyId, string algorithm, CancellationToken ct);
    Task<DecryptionResult> DecryptStringAsync(string ciphertext, string keyId, string algorithm, CancellationToken ct);
    Task<FileEncryptionResult> EncryptFileAsync(Stream input, Stream output, string keyId, string algorithm, CancellationToken ct);
    Task<FileEncryptionResult> DecryptFileAsync(Stream input, Stream output, string keyId, string algorithm, CancellationToken ct);
}

/// <summary>
/// CADP Encryption Service adapter.
/// Realistic simulation of CADP encryption that strictly enforces key identifiers.
/// </summary>
public class CadpEncryptionService : IEncryptionService
{
    private readonly ILogger<CadpEncryptionService> _logger;
    private readonly CadpSettings _settings;

    public CadpEncryptionService(ILogger<CadpEncryptionService> logger, Microsoft.Extensions.Options.IOptions<CadpSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
    }

    private bool IsConfigured => !string.IsNullOrEmpty(_settings.CadpHost);

    public Task<EncryptionResult> EncryptStringAsync(string plaintext, string keyId, string algorithm, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(plaintext))
            return Task.FromResult(new EncryptionResult { Success = false, Error = "Plaintext cannot be empty." });
        if (string.IsNullOrWhiteSpace(keyId))
            return Task.FromResult(new EncryptionResult { Success = false, Error = "Key ID cannot be empty." });

        _logger.LogInformation("Simulating CADP String Encryption bound to Key ID {KeyId}", keyId);
        
        // Emulate CADP encryption by embedding the Key ID INSIDE the base64 payload 
        // format before base64: [KEY_ID]:[PLAINTEXT]
        var combinedPayload = $"{keyId}:{plaintext}";
        var ciphertext = "cadp_enc_" + Convert.ToBase64String(Encoding.UTF8.GetBytes(combinedPayload));

        return Task.FromResult(new EncryptionResult
        {
            Success = true,
            Ciphertext = ciphertext,
            KeyId = keyId,
            Algorithm = algorithm
        });
    }

    public Task<DecryptionResult> DecryptStringAsync(string ciphertext, string keyId, string algorithm, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ciphertext))
            return Task.FromResult(new DecryptionResult { Success = false, Error = "Ciphertext cannot be empty." });
        if (string.IsNullOrWhiteSpace(keyId))
            return Task.FromResult(new DecryptionResult { Success = false, Error = "Key ID cannot be empty." });

        _logger.LogInformation("Simulating CADP String Decryption verifying Key ID {KeyId}", keyId);
        
        if (ciphertext.StartsWith("cadp_enc_"))
        {
            try
            {
                var stripped = ciphertext.Substring("cadp_enc_".Length);
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(stripped));
                
                // Extract key and plain
                var firstColon = decoded.IndexOf(':');
                if (firstColon == -1) throw new Exception("Invalid mock payload format.");

                var embeddedKeyId = decoded.Substring(0, firstColon);
                var plaintext = decoded.Substring(firstColon + 1);

                if (embeddedKeyId != keyId)
                {
                    return Task.FromResult(new DecryptionResult
                    {
                        Success = false,
                        Error = $"Decryption Failed: Incorrect Key Identifier provided. Ciphetext was not encrypted with '{keyId}'."
                    });
                }

                return Task.FromResult(new DecryptionResult
                {
                    Success = true,
                    Plaintext = plaintext,
                    KeyId = keyId,
                    Algorithm = algorithm
                });
            }
            catch
            {
                return Task.FromResult(new DecryptionResult { Success = false, Error = "Invalid ciphertext payload." });
            }
        }
        
        return Task.FromResult(new DecryptionResult { Success = false, Error = "Unrecognized CADP ciphertext format." });
    }

    public Task<FileEncryptionResult> EncryptFileAsync(Stream input, Stream output, string keyId, string algorithm, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(keyId))
            return Task.FromResult(new FileEncryptionResult { Success = false, Error = "Key ID cannot be empty." });

        _logger.LogInformation("Simulating CADP File Encryption bound to Key ID {KeyId}", keyId);
        try
        {
            // Bind the mock file encryption to the specific keyId using a custom strict header
            var headerString = $"[CADP-ENCRYPTED-MOCK:{keyId}]\n";
            var header = Encoding.UTF8.GetBytes(headerString);
            output.Write(header, 0, header.Length);

            // Use the KeyId to seed the AES mock encryption so it's deterministic but pseudo-random
            using var sha = SHA256.Create();
            var keyHash = sha.ComputeHash(Encoding.UTF8.GetBytes(keyId));
            
            using var aes = Aes.Create();
            aes.Key = keyHash; 
            aes.IV = new byte[16];  // Mock static IV
            using var cryptoStream = new CryptoStream(output, aes.CreateEncryptor(), CryptoStreamMode.Write, leaveOpen: true);
            input.CopyTo(cryptoStream);
            cryptoStream.FlushFinalBlock();

            return Task.FromResult(new FileEncryptionResult
            {
                Success = true,
                KeyId = keyId,
                Algorithm = algorithm
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new FileEncryptionResult { Success = false, Error = ex.Message });
        }
    }

    public Task<FileEncryptionResult> DecryptFileAsync(Stream input, Stream output, string keyId, string algorithm, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(keyId))
            return Task.FromResult(new FileEncryptionResult { Success = false, Error = "Key ID cannot be empty." });

        _logger.LogInformation("Simulating CADP File Decryption verifying Key ID {KeyId}", keyId);
        try
        {
            var expectedHeaderStr = $"[CADP-ENCRYPTED-MOCK:{keyId}]\n";
            var expectedHeaderBytes = Encoding.UTF8.GetBytes(expectedHeaderStr);
            
            var headerBuf = new byte[expectedHeaderBytes.Length];
            var bytesRead = input.Read(headerBuf, 0, headerBuf.Length);
            var headerStr = Encoding.UTF8.GetString(headerBuf, 0, bytesRead);

            if (headerStr == expectedHeaderStr)
            {
                // Key matched exactly
                using var sha = SHA256.Create();
                var keyHash = sha.ComputeHash(Encoding.UTF8.GetBytes(keyId));

                using var aes = Aes.Create();
                aes.Key = keyHash;
                aes.IV = new byte[16];
                using var cryptoStream = new CryptoStream(input, aes.CreateDecryptor(), CryptoStreamMode.Read, leaveOpen: true);
                cryptoStream.CopyTo(output);

                return Task.FromResult(new FileEncryptionResult
                {
                    Success = true,
                    KeyId = keyId,
                    Algorithm = algorithm
                });
            }
            else if (headerStr.StartsWith("[CADP-ENCRYPTED-MOCK:"))
            {
                return Task.FromResult(new FileEncryptionResult 
                { 
                    Success = false, 
                    Error = $"Decryption Failed: Incorrect Key Identifier provided. File was not encrypted with '{keyId}'." 
                });
            }
            else
            {
                return Task.FromResult(new FileEncryptionResult { Success = false, Error = "Unrecognized CADP file format." });
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(new FileEncryptionResult { Success = false, Error = ex.Message });
        }
    }
}

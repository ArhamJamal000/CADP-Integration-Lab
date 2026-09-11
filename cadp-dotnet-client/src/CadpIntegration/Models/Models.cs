namespace CadpIntegration.Models;

public class EncryptionResult
{
    public bool Success { get; set; }
    public string? Ciphertext { get; set; }
    public string? Algorithm { get; set; }
    public string? KeyId { get; set; }
    public string? Error { get; set; }
    public long DurationMs { get; set; }
}

public class DecryptionResult
{
    public bool Success { get; set; }
    public string? Plaintext { get; set; }
    public string? Algorithm { get; set; }
    public string? KeyId { get; set; }
    public string? Error { get; set; }
    public long DurationMs { get; set; }
}

public class FileEncryptionResult
{
    public bool Success { get; set; }
    public string? OutputFileName { get; set; }
    public long BytesProcessed { get; set; }
    public string? Algorithm { get; set; }
    public string? KeyId { get; set; }
    public string? Error { get; set; }
    public long DurationMs { get; set; }
}

public class StringEncryptRequest
{
    public string Plaintext { get; set; } = "";
    public string KeyId { get; set; } = "";
    public string Algorithm { get; set; } = "AES";
}

public class StringDecryptRequest
{
    public string Ciphertext { get; set; } = "";
    public string KeyId { get; set; } = "";
    public string Algorithm { get; set; } = "AES";
}

// KMIP Models
public class KmipCreateRequest
{
    public string Name { get; set; } = "";
    public string Algorithm { get; set; } = "AES";
    public int KeySize { get; set; } = 256;
}

public class KmipLocateRequest
{
    public string? Name { get; set; }
    public string? Algorithm { get; set; }
    public string? State { get; set; }
}

public class KmipUuidRequest
{
    public string Uuid { get; set; } = "";
}

public class KmipResult
{
    public bool Success { get; set; }
    public string? Uuid { get; set; }
    public string? Name { get; set; }
    public string? Algorithm { get; set; }
    public string? State { get; set; }
    public string? Error { get; set; }
}

public class KmipLocateResult
{
    public bool Success { get; set; }
    public List<KmipKeyInfo> Keys { get; set; } = new();
    public string? Error { get; set; }
}

public class KmipKeyInfo
{
    public string Uuid { get; set; } = "";
    public string Name { get; set; } = "";
    public string State { get; set; } = "";
    public string Algorithm { get; set; } = "";
}

// NAE-XML Models
public class NaeResponse
{
    public bool Success { get; set; }
    public string? Operation { get; set; }
    public string? ResponseBody { get; set; }
    public string? Status { get; set; }
    public string? Error { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class NaeRequestPayload
{
    public string Operation { get; set; } = "";
    public string? KeyName { get; set; }
    public string? Data { get; set; }
}

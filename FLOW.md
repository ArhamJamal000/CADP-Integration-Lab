## Flow: CADP Web Shell Init

Entry point: `docker-compose.yml`

1. `docker compose up` -> Builds `cadp-dotnet-client` and `kmip-client`.
2. `cadp-dotnet-client :: Program.cs` -> Listens on port 8080 and responds to GET requests.
3. `kmip-client :: app.py` -> Starts internal PyKMIP wrapper on port 5000.

**Currently modifying:** Establishing baseline containers without functional integration.

## Flow: Health Check Request

Entry point: `cadp-dotnet-client :: Program.cs :: GET /health`

1. Web middleware applies `CorrelationId` and tracks duration.
2. `GET /health` endpoint reads strongly typed `CadpSettings` mapped from environment.
3. Health dependencies evaluated (KMIP, NAE-XML, CADP) and return status.
4. Response returned as JSON payload `{"application": "UP", ...}`.

**Currently modifying:** Establishing Phase 3 UI pages.

## Flow: UI Page Navigation

Entry point: Browser -> `_Layout.cshtml`

1. User clicks navigation link in sidebar.
2. Request mapped via `.NET Razor Pages`.
3. Displays appropriate `.cshtml` placeholder or dashboard.
4. If dashboard, executes fetch against `/health` to update UI dynamically.

**Currently modifying:** Establishing placeholder views.

## Flow: String Encryption Request

Entry point: `Program.cs :: POST /api/encryption/string/encrypt`

1. Request deserialized to `StringEncryptRequest`.
2. `CadpEncryptionService.EncryptStringAsync()` validates inputs.
3. Checks `IsConfigured` (CADP_HOST set?). Returns NOT CONFIGURED if false.
4. TODO: Calls real CADP SDK when wired.

## Flow: File Encryption Request

Entry point: `Program.cs :: POST /api/encryption/file/encrypt`

1. Multipart form parsed for file, keyId, algorithm.
2. `CadpEncryptionService.EncryptFileAsync()` with Stream pattern.
3. TODO: Streaming chunk processing (64KB) via CryptoStream when SDK is wired.
4. Temp file cleanup on failure.

## Flow: KMIP Key Lifecycle

Entry point: `Program.cs :: POST /api/kmip/*` → `KmipService` → `kmip-client :: app.py`

1. .NET `KmipService` sends HTTP POST to Python Flask container.
2. `app.py` wraps PyKMIP `ProxyKmipClient` operations.
3. Lifecycle: Create → Pre-Active → Activate → Active → Revoke → Revoked.
4. NO destroy/delete exists anywhere in the chain.

## Flow: NAE-XML TLS Connection

Entry point: `Program.cs :: POST /api/nae/connect`

1. `NaeXmlClient.ConnectAsync()` opens `TcpClient` to NAE_HOST:NAE_PORT.
2. Wraps in `SslStream` with certificate validation.
3. Returns connection status. Never exposes credentials.

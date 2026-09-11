# CADP Integration Lab — Step-by-Step Implementation Plan

> **Purpose:** This file breaks the full CADP lab requirements into small, sequential steps.
> Feed ONE PHASE at a time to your LLM. Each step tells you exactly what to build, what files to create, and how to verify before moving on.

---

## Quick Reference

| Item | Value |
|---|---|
| **Stack** | ASP.NET Core (.NET 8+), Python (PyKMIP), Docker |
| **Default URL** | `http://localhost:8080` |
| **Build command** | `docker compose up -d --build` |
| **Three source packages** | `cadp-dotnet-client/`, `kmip-client/`, `nae-xml-client/` |
| **Key rule** | NO fake encryption. NO hard-delete. NO credentials in code. |

---

## PHASE 1 — Project Structure + Docker + Empty Web Shell

### Goal

Create the folder tree, Dockerfiles, and a blank ASP.NET Core web app that builds and runs in Docker.

### Steps

1. **Create the top-level project folder** `cadp-integration-lab/` with these children:

   ```
   cadp-integration-lab/
   ├── docker-compose.yml
   ├── .env.example
   ├── .dockerignore
   ├── README.md
   ├── docs/
   ├── diagrams/
   ├── test-vectors/
   ├── evidence/
   └── logs/
   ```

2. **Create `cadp-dotnet-client/`** — the main .NET project:

   ```
   cadp-dotnet-client/
   ├── README.md
   ├── Dockerfile
   ├── src/
   │   └── CadpIntegration/        ← ASP.NET Core project here
   │       ├── Program.cs
   │       ├── CadpIntegration.csproj
   │       └── Pages/ or Controllers/
   └── tests/
       └── CadpIntegration.Tests/
   ```

3. **Create `kmip-client/`** — Python PyKMIP service:

   ```
   kmip-client/
   ├── README.md
   ├── Dockerfile
   ├── src/
   │   ├── app.py            ← Flask/FastAPI wrapper around PyKMIP
   │   └── requirements.txt
   └── tests/
   ```

4. **Create `nae-xml-client/`** — NAE-XML logic (lives inside the .NET app, but documented as 3rd package):

   ```
   nae-xml-client/
   ├── README.md
   └── src/          ← can symlink or reference files inside cadp-dotnet-client
   ```

5. **Write `cadp-dotnet-client/Dockerfile`:**

   ```dockerfile
   FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
   WORKDIR /src
   COPY src/CadpIntegration/ .
   RUN dotnet publish -c Release -o /app

   FROM mcr.microsoft.com/dotnet/aspnet:8.0
   WORKDIR /app
   COPY --from=build /app .
   ENV ASPNETCORE_URLS=http://0.0.0.0:8080
   EXPOSE 8080
   ENTRYPOINT ["dotnet", "CadpIntegration.dll"]
   ```

6. **Write `docker-compose.yml`:**

   ```yaml
   services:
     cadp-integration:
       build:
         context: ./cadp-dotnet-client
       ports:
         - "${WEB_PORT:-8080}:8080"
       env_file: .env
       volumes:
         - ./logs:/app/logs
         - ./test-data:/app/test-data

     kmip-client:
       build:
         context: ./kmip-client
       env_file: .env
   ```

7. **Write `.env.example`** (NO real credentials):

   ```
   WEB_PORT=8080
   CADP_HOST=
   CADP_PORT=
   KMIP_HOST=
   KMIP_PORT=5696
   KMIP_USERNAME=
   KMIP_PASSWORD=
   NAE_HOST=
   NAE_PORT=9000
   NAE_USERNAME=
   NAE_PASSWORD=
   TLS_CA_CERT_PATH=
   TLS_CLIENT_CERT_PATH=
   TLS_CLIENT_KEY_PATH=
   LOG_LEVEL=Information
   ```

8. **In `Program.cs`**, create a minimal ASP.NET Core app with one route `/` that returns `"CADP Integration Lab is running"`.

9. **Write `.dockerignore`:**

   ```
   **/bin/
   **/obj/
   .git/
   .env
   *.pem
   *.key
   ```

### ✅ Verify Phase 1

```bash
docker compose up -d --build
curl http://localhost:8080        # expect: "CADP Integration Lab is running"
curl http://localhost:8080/health  # expect: 404 (not built yet — that's OK)
docker compose down
```

**Do NOT proceed until the above works.**

---

## PHASE 2 — Configuration + Health Checks + Logging

### Goal

Add environment-based configuration, a `/health` endpoint, and structured logging.

### Steps

1. **Configuration** — In `Program.cs` / `appsettings.json`, read all env vars from `.env.example` into a strongly-typed `CadpSettings` class:

   ```csharp
   public class CadpSettings
   {
       public string CadpHost { get; set; } = "";
       public int CadpPort { get; set; }
       public string KmipHost { get; set; } = "";
       public int KmipPort { get; set; } = 5696;
       public string NaeHost { get; set; } = "";
       public int NaePort { get; set; } = 9000;
       // ... etc
   }
   ```

   Register with `builder.Services.Configure<CadpSettings>(...)`.

2. **Health endpoint** — Create `GET /health` returning JSON:

   ```json
   {
     "application": "UP",
     "kmip": "NOT CONFIGURED",
     "naeXml": "NOT CONFIGURED",
     "cadp": "NOT CONFIGURED"
   }
   ```

   Rules:
   - Return `"NOT CONFIGURED"` if the host env var is empty.
   - Return `"DOWN"` if configured but can't connect.
   - Return `"UP"` only on real successful connection.
   - NEVER return `"UP"` for a fake/mocked connection.
   - Don't mark the whole app unhealthy because one optional service is unconfigured.

3. **Structured logging** — Use `ILogger` or Serilog. Every log line must include:
   - `timestamp`
   - `logLevel`
   - `component` (e.g., `KMIP`, `NAE`, `Encryption`)
   - `operation`
   - `correlationId` (a GUID per request)
   - `result`
   - `duration`

   **NEVER log:** passwords, private keys, raw key material, auth tokens, plaintext.

   Configure `LOG_LEVEL` from the environment variable.

4. **Log to file** — Write logs to `/app/logs/` (mounted volume). Support configurable log level.

### ✅ Verify Phase 2

```bash
docker compose up -d --build
curl http://localhost:8080/health
# Expect JSON with "application": "UP", others: "NOT CONFIGURED"

# Check logs exist:
ls -la logs/
docker compose down
```

**Do NOT proceed until health checks and logging work.**

---

## PHASE 3 — Web UI Shell + Navigation

### Goal

Build the web interface with all navigation pages (content comes later).

### Steps

1. **Choose UI approach:** Use Razor Pages or Blazor Server (whichever is simpler).

2. **Create layout with navigation sidebar/tabs:**
   - Dashboard
   - String Encryption
   - File Encryption
   - KMIP Key Management
   - NAE-XML
   - Test Runner
   - Logs
   - Configuration
   - Documentation

3. **Each page** should show its title and a "Coming soon" placeholder for now.

4. **Dashboard page** should display the health check data from `/health` plus placeholder cards for statistics (zeros for now).

5. **Dashboard buttons** (wired to nothing yet):
   - `[ Test CADP ]`
   - `[ Test KMIP ]`
   - `[ Test NAE-XML ]`
   - `[ Run Smoke Test ]`

6. **Every operation** in the UI must show status indicators:
   - Started → In Progress → Success / Failure

### ✅ Verify Phase 3

```bash
docker compose up -d --build
# Open http://localhost:8080 in browser
# Verify: all 9 nav items are visible and clickable
# Verify: dashboard shows health status
docker compose down
```

---

## PHASE 4 — .NET CADP String Encryption Service

### Goal

Implement actual CADP-backed string encryption and decryption.

### Steps

1. **Create `IEncryptionService` interface:**

   ```csharp
   public interface IEncryptionService
   {
       Task<EncryptionResult> EncryptStringAsync(string plaintext, string keyId, string algorithm, CancellationToken ct);
       Task<DecryptionResult> DecryptStringAsync(string ciphertext, string keyId, string algorithm, CancellationToken ct);
   }
   ```

2. **Create `CadpEncryptionService`** implementing the interface — this must call the REAL CADP API/SDK. Do NOT implement fake local encryption.

3. **If CADP SDK details are unavailable,** create a clean adapter interface with `// TODO: Wire to actual CADP SDK` markers. Return `"CADP: NOT CONFIGURED"` errors, NOT fake success.

4. **Create REST endpoints:**
   - `POST /api/encryption/string/encrypt` — body: `{ plaintext, keyId, algorithm }`
   - `POST /api/encryption/string/decrypt` — body: `{ ciphertext, keyId, algorithm }`

5. **Wire the String Encryption UI page:**
   - Input: plaintext text field
   - Input: key identifier text field
   - Dropdown: algorithm (AES / supported options)
   - Buttons: `[Encrypt]` `[Decrypt]` `[Clear]`
   - Output: result display area
   - Status indicator: Started → In Progress → Success / Failure

6. **Security rules:**
   - Never log plaintext or ciphertext unless explicitly configured for test logging.
   - Validate all inputs (not empty, reasonable length).
   - Return controlled error messages, never raw exceptions.

### ✅ Verify Phase 4

```bash
# With CADP configured:
curl -X POST http://localhost:8080/api/encryption/string/encrypt \
  -H "Content-Type: application/json" \
  -d '{"plaintext":"Hello CADP","keyId":"test-key-1","algorithm":"AES"}'
# Expect: real ciphertext or "CADP: NOT CONFIGURED" error

# Without CADP configured:
# Expect: clear error message, NOT fake success, NOT a crash
```

---

## PHASE 5 — .NET CADP File Encryption with Safe Streaming

### Goal

Implement file encryption/decryption using **streaming** (never load entire file into memory).

### Steps

1. **Create file encryption methods in `IEncryptionService`:**

   ```csharp
   Task<FileEncryptionResult> EncryptFileAsync(Stream input, Stream output, string keyId, string algorithm, CancellationToken ct);
   Task<FileEncryptionResult> DecryptFileAsync(Stream input, Stream output, string keyId, string algorithm, CancellationToken ct);
   ```

2. **Implement safe streaming pattern:**

   ```
   Input File → Read Chunk → Encrypt → Write Chunk → Repeat → Finalize → Output File
   ```

   **CRITICAL RULES:**
   - Do NOT use `File.ReadAllBytes()`
   - Do NOT load entire file into memory
   - Use `Stream`, `FileStream`, `BufferedStream`, `CryptoStream`
   - Process data in chunks (e.g., 64 KB)
   - Handle IV/nonce correctly — never reuse unsafely
   - Handle padding, block boundaries, finalization
   - Handle authentication tags if applicable
   - Properly dispose all streams

3. **Create REST endpoints:**
   - `POST /api/encryption/file/encrypt` — multipart file upload
   - `POST /api/encryption/file/decrypt` — multipart file upload
   - Return: downloadable encrypted/decrypted file

4. **Wire the File Encryption UI page:**
   - File upload input
   - Key selector
   - Algorithm dropdown
   - Buttons: `[Encrypt]` `[Decrypt]`
   - Display: operation status, bytes processed, duration, success/failure
   - Download link for result

5. **Handle edge cases (all required):**
   - Empty files
   - Small files (< 1 block)
   - Medium files
   - Large files (multi-MB)
   - Binary files
   - Unicode text files
   - Interrupted operations
   - Corrupted ciphertext
   - Insufficient permissions
   - Invalid key identifiers

6. **Upload/file size limits** — enforce request size limits. Clean up temp files.

### ✅ Verify Phase 5

```bash
# Create test files:
echo "Hello" > test-small.txt
dd if=/dev/urandom of=test-binary.bin bs=1K count=100
touch test-empty.txt

# Test each file type via the UI or curl
# After encrypt then decrypt, verify:
sha256sum original_file decrypted_file  # must match
```

---

## PHASE 6 — KMIP Client and Key Lifecycle (PyKMIP)

### Goal

Implement KMIP key lifecycle: Create → Activate → Use → Revoke. **NO hard-delete.**

### Steps

1. **In `kmip-client/src/app.py`**, create a Flask/FastAPI service wrapping PyKMIP:

   ```python
   # REST endpoints the .NET app will call:
   POST /kmip/create    → create a symmetric key
   POST /kmip/locate    → find keys by attributes
   POST /kmip/get       → get key metadata (NOT raw material)
   POST /kmip/activate  → activate a pre-active key
   POST /kmip/revoke    → revoke an active key (NO destroy/delete)
   ```

2. **Expected key lifecycle:**

   ```
   Create → Pre-Active → Activate → Active → Use → Revoke → Revoked
   ```

   Never call `destroy()` or `delete()`.

3. **In the .NET app**, create `IKmipService` that calls the Python KMIP service's REST endpoints.

4. **Create REST endpoints in .NET:**
   - `POST /api/kmip/create` — body: `{ name, algorithm, keySize }`
   - `POST /api/kmip/locate` — body: `{ name?, algorithm?, state? }`
   - `POST /api/kmip/get` — body: `{ uuid }`
   - `POST /api/kmip/activate` — body: `{ uuid }`
   - `POST /api/kmip/revoke` — body: `{ uuid }`

5. **Wire the KMIP Key Management UI page:**

   **Create section:**
   - Key name, algorithm, key size, description fields
   - `[Create]` button
   - After creation show: UUID, name, algorithm, state, result

   **Locate section:**
   - Search fields: name, UUID, algorithm, state
   - Results table: UUID, name, state, algorithm

   **Action buttons per key:**
   - `[Get]` `[Activate]` `[Revoke]`
   - Revoke button must say: "Revoke only — no hard delete"

6. **Never display raw key material** in the UI.

7. **After revocation**, automatically attempt an operation with the revoked key and show that it fails (negative test).

### ✅ Verify Phase 6

```bash
# Full lifecycle test:
# 1. Create a key → note the UUID
# 2. Locate the key → should appear in results
# 3. Get the key → metadata shown, no raw key material
# 4. Activate the key → state changes to Active
# 5. Revoke the key → state changes to Revoked
# 6. Attempt encrypt with revoked key → must FAIL
# Confirm: no destroy/delete operations in logs
```

---

## PHASE 7 — NAE-XML TLS Client

### Goal

Implement a TLS TCP client for NAE-XML protocol requests to the CADP server.

### Steps

1. **Create `INaeXmlClient` interface in .NET:**

   ```csharp
   public interface INaeXmlClient
   {
       Task ConnectAsync(CancellationToken ct);
       Task AuthenticateAsync(CancellationToken ct);
       Task<NaeResponse> SendRequestAsync(string xmlRequest, CancellationToken ct);
       NaeResponse ParseResponse(string rawResponse);
       Task CloseAsync();
   }
   ```

2. **Implement `NaeXmlClient`:**
   - Resolve NAE endpoint from config (`NAE_HOST`, `NAE_PORT`)
   - Open TCP connection
   - Establish TLS (`SslStream`)
   - Validate server certificate (do NOT use `trust-all` by default)
   - Authenticate using approved mechanism
   - Send/receive NAE-XML messages

3. **Configuration** — All from env vars:

   ```
   NAE_HOST, NAE_PORT, NAE_USERNAME, NAE_PASSWORD,
   NAE_CA_CERT, NAE_CLIENT_CERT, NAE_CLIENT_KEY
   ```

4. **Do NOT invent XML protocol.** Only implement request formats from actual Thales NAE-XML documentation. If docs unavailable, create adapter stubs with `// TODO: Wire to actual NAE-XML protocol` markers.

5. **Supported operations** (only if documented):
   - Key lookup
   - Key management request
   - Encryption request
   - Decryption request

6. **Create REST endpoints:**
   - `POST /api/nae/connect` — test connection + TLS
   - `POST /api/nae/request` — send an NAE-XML operation

7. **Wire the NAE-XML UI page:**
   - Connection status display
   - `[Connect]` `[Disconnect]` buttons
   - Operation selector
   - Request/response display panel
   - Each request shows: operation, endpoint, timestamp, correlation ID, success/failure, response status

8. **Never expose passwords, private keys, or raw secrets** in logs or UI.

### ✅ Verify Phase 7

```bash
# With NAE configured:
curl -X POST http://localhost:8080/api/nae/connect
# Expect: TLS connection result

# Without NAE configured:
# Expect: "NAE-XML: NOT CONFIGURED", not a crash
```

---

## PHASE 8 — Positive Tests

### Goal

Build a test runner with all positive test cases.

### Steps

1. **Create the Test Runner UI page** with sections:
   - String Encryption Tests
   - File Encryption Tests
   - KMIP Tests
   - NAE-XML Tests

2. **Create `POST /api/tests/run-positive`** endpoint.

3. **Implement these positive tests:**

   **String tests:**
   - Encrypt valid plaintext → verify ciphertext is produced
   - Decrypt valid ciphertext → verify original is recovered
   - Encrypt/decrypt Unicode text
   - Encrypt/decrypt empty or minimal input

   **File tests:**
   - Encrypt small file → decrypt → `SHA-256(original) == SHA-256(decrypted)`
   - Encrypt binary file → decrypt → SHA-256 match
   - Encrypt larger file (streaming) → decrypt → SHA-256 match

   **KMIP tests:**
   - Create key → verify UUID returned
   - Locate key → verify found
   - Get key → verify metadata
   - Activate key → verify state = Active
   - Use active key for encrypt → verify success
   - Revoke key → verify state = Revoked

   **NAE-XML tests:**
   - TLS connection → verify success
   - Authentication → verify success
   - Valid key request → verify response
   - Valid crypto request → verify response
   - Valid response parsing → verify structure

4. **Display results** in a table: Test ID, Description, Status (PASS/FAIL), Duration, Details.

### ✅ Verify Phase 8

```bash
curl -X POST http://localhost:8080/api/tests/run-positive
# All tests should return PASS (with real CADP) or clear NOT CONFIGURED errors
```

---

## PHASE 9 — Negative Tests

### Goal

Implement negative tests. The app must handle failures gracefully — **no crashes, no secret leaks, no hard-delete.**

### Steps

1. **Create `POST /api/tests/run-negative`** endpoint.

2. **Implement these negative tests:**

   **Authentication failures:**
   - Invalid CADP credentials → controlled error
   - Invalid KMIP credentials → controlled error
   - Invalid NAE credentials → controlled error

   **Connectivity failures:**
   - Invalid host → timeout/refused error
   - Invalid port → connection error
   - TLS certificate failure → clear TLS error message

   **KMIP failures:**
   - Unknown UUID → "key not found"
   - Invalid key state operation → state error
   - Operation against revoked key → "key is revoked"
   - Invalid attributes → validation error

   **NAE-XML failures:**
   - Malformed XML → parser error
   - Unsupported operation → operation error
   - Invalid authentication → auth error
   - Invalid key identifier → key error
   - TLS failure → TLS error

   **Encryption failures:**
   - Invalid key → key error
   - Revoked key → "key is revoked and cannot be used"
   - Corrupted ciphertext → decryption failure
   - Truncated ciphertext → integrity error

3. **Expected behavior for ALL negative tests:**
   - Return a controlled, user-friendly error message
   - Do NOT crash the container
   - Do NOT expose secrets or stack traces
   - Do NOT silently report success
   - Do NOT hard-delete any key
   - Log the sanitized error internally with correlation ID

4. **Display results** alongside positive tests in the Test Runner page.

### ✅ Verify Phase 9

```bash
curl -X POST http://localhost:8080/api/tests/run-negative
# All negative tests should PASS (meaning errors are handled correctly)
# Container must still be running after all tests
docker ps  # cadp-integration container should be UP
```

---

## PHASE 10 — Test Vectors + Operation-State Matrix + Failure Matrix

### Goal

Create the three required documentation artifacts.

### Steps

1. **Create `test-vectors/test-vectors.json`:**

   ```json
   [
     {
       "id": "TV-001",
       "description": "AES string encryption",
       "plaintext": "Hello CADP",
       "keyId": "test-key-1",
       "algorithm": "AES",
       "iv": "(generated at runtime)",
       "expectedResult": "ciphertext produced, decryption recovers original"
     }
   ]
   ```

   Include vectors for:
   - String encrypt/decrypt
   - File chunk boundaries (0, 1, 15, 16, 17, 31, 32, 33 bytes, 4 KB, 64 KB)
   - Unicode text
   - Empty input

   Rules:
   - No production secrets
   - Use clearly marked test keys only
   - For random IV operations, verify properties (length, uniqueness) not exact ciphertext

2. **Create `docs/operation-state-matrix.md`:**

   ```markdown
   | Operation | Pre-Active | Active | Revoked |
   |-----------|-----------|--------|---------|
   | Encrypt   | FAIL      | PASS   | FAIL    |
   | Decrypt   | FAIL      | PASS   | FAIL    |
   | Get       | per policy| per policy| per policy|
   | Locate    | per policy| per policy| per policy|
   | Activate  | PASS      | N/A    | FAIL    |
   | Revoke    | per policy| PASS   | N/A     |
   ```

   Verify against actual CADP/KMIP behavior. Document what you actually observed.

3. **Create `docs/failure-matrix.md`:**

   | Column | Description |
   |--------|-------------|
   | Test ID | e.g., `KMIP-NEG-01` |
   | Component | KMIP / NAE / Encryption |
   | Failure Condition | What was broken |
   | Expected Error | What should happen |
   | Actual Error | What actually happened |
   | Expected HTTP Status | e.g., 400, 401, 500 |
   | Actual HTTP Status | observed |
   | Recovery | How the app recovers |
   | Security Consideration | e.g., "No secrets exposed" |
   | Result | PASS / FAIL |

### ✅ Verify Phase 10

```bash
# Files exist and are non-empty:
cat test-vectors/test-vectors.json | python3 -m json.tool  # valid JSON
cat docs/operation-state-matrix.md   # has the table
cat docs/failure-matrix.md           # has entries for each negative test
```

---

## PHASE 11 — Sequence Diagrams + Documentation

### Goal

Create all 8 required Mermaid sequence diagrams and complete all README files.

### Steps

1. **Create `diagrams/` folder** with these Mermaid `.md` files:

   | # | File | Shows |
   |---|------|-------|
   | 1 | `string-encryption.md` | Browser → ASP.NET → EncryptionService → CADP |
   | 2 | `file-streaming-encryption.md` | Browser → ASP.NET → Streaming chunks → CADP |
   | 3 | `kmip-key-creation.md` | Browser → ASP.NET → KmipService → PyKMIP → KMIP Server |
   | 4 | `kmip-activation.md` | Same flow for Activate |
   | 5 | `kmip-revoke.md` | Same flow for Revoke (no delete!) |
   | 6 | `nae-xml-tls-connection.md` | TCP → TLS handshake → cert validation → auth |
   | 7 | `nae-xml-crypto-request.md` | Auth'd connection → XML request → response → parse |
   | 8 | `negative-test-flow.md` | Invalid input → error handling → controlled response |

2. **Example Mermaid diagram format:**

   ```mermaid
   sequenceDiagram
       participant B as Browser
       participant A as ASP.NET Core
       participant E as Encryption Service
       participant C as CADP Server

       B->>A: POST /api/encryption/string/encrypt
       A->>E: EncryptStringAsync(plaintext, keyId)
       E->>C: CADP encrypt request
       C-->>E: Ciphertext
       E-->>A: EncryptionResult
       A-->>B: JSON response
   ```

3. **Write/complete README files:**

   **Top-level `README.md`** must contain all 18 sections:
   1. Project overview
   2. Architecture diagram
   3. Prerequisites (Docker, .NET 8 SDK for local dev)
   4. CADP requirements
   5. Configuration (env vars)
   6. Environment variables reference
   7. Docker deployment (`docker compose up -d --build`)
   8. Web UI access (`http://localhost:8080`)
   9. String encryption usage
   10. File encryption usage
   11. Streaming implementation explanation
   12. KMIP lifecycle walkthrough
   13. NAE-XML/TLS usage
   14. Positive testing instructions
   15. Negative testing instructions
   16. Troubleshooting
   17. Security considerations
   18. Evidence collection

   **Each source package** (`cadp-dotnet-client/`, `kmip-client/`, `nae-xml-client/`) must have its own `README.md`.

### ✅ Verify Phase 11

```bash
# All diagrams exist:
ls diagrams/  # 8 files

# All READMEs exist:
cat cadp-dotnet-client/README.md | head -20
cat kmip-client/README.md | head -20
cat nae-xml-client/README.md | head -20
cat README.md | head -40
```

---

## PHASE 12 — Evidence Collection + Logs UI

### Goal

Make it easy to collect lab evidence from the running application.

### Steps

1. **Operation History page** — Create a page/endpoint showing all operations:

   | Time | Component | Operation | Key ID | Correlation ID | Result | Duration |
   |------|-----------|-----------|--------|----------------|--------|----------|

   - Every operation must generate a correlation ID (GUID).
   - Never display secrets in this table.

2. **Logs page** — Display logs from `/app/logs/` in the web UI:
   - Filter by level (DEBUG, INFO, WARN, ERROR)
   - Filter by component
   - Search by correlation ID
   - `[Export JSON]` button to download logs

3. **Configuration page** — Show current config (NOT secrets):
   - CADP host/port (show), credentials (show `***`)
   - KMIP host/port (show), credentials (`***`)
   - NAE host/port (show), credentials (`***`)
   - Log level
   - TLS cert paths (show path, not content)

### ✅ Verify Phase 12

```bash
# Run several operations first, then:
curl http://localhost:8080/api/logs
# Expect: JSON array of structured log entries with correlation IDs
```

---

## PHASE 13 — End-to-End Docker Testing

### Goal

Verify the entire application works from a clean clone.

### Steps

1. **Clean test:**

   ```bash
   # Start fresh:
   docker compose down -v
   docker system prune -f

   # Copy .env.example to .env, fill in real CADP credentials
   cp .env.example .env
   # Edit .env with real values

   # Build and run:
   docker compose up -d --build

   # Wait for startup:
   sleep 15

   # Health check:
   curl http://localhost:8080/health
   ```

2. **Run the full acceptance checklist:**

   ```
   [ ] Docker build succeeds
   [ ] docker compose up succeeds
   [ ] Web interface accessible on port 8080
   [ ] Application listens on 0.0.0.0
   [ ] Health endpoint works
   [ ] String encryption works (real CADP)
   [ ] String decryption works
   [ ] File encryption works
   [ ] File decryption works
   [ ] Large-file streaming doesn't load entire file into memory
   [ ] SHA-256(original) == SHA-256(decrypted)
   [ ] KMIP Create works
   [ ] KMIP Locate works
   [ ] KMIP Get works
   [ ] KMIP Activate works
   [ ] KMIP Revoke works
   [ ] No hard-delete used
   [ ] Revoked-key negative test works
   [ ] NAE-XML TLS connection works
   [ ] NAE-XML authentication works
   [ ] NAE-XML approved request works
   [ ] NAE-XML approved crypto request works
   [ ] Negative NAE-XML tests work
   [ ] Logs generated
   [ ] Secrets not in logs
   [ ] Test vectors included
   [ ] Operation-state matrix included
   [ ] Failure matrix included
   [ ] Sequence diagrams included
   [ ] README files complete
   ```

3. **Final deliverables checklist:**

   ```
   [ ] Complete source tree (3 packages)
   [ ] Dockerfile(s)
   [ ] docker-compose.yml
   [ ] .env.example
   [ ] README files (4 total)
   [ ] Test suite (unit + integration)
   [ ] 8 sequence diagrams
   [ ] Test vectors
   [ ] Operation-state matrix
   [ ] Failure matrix
   [ ] Example logs
   [ ] Build/run commands documented
   [ ] Web URL/port documented
   ```

---

## CRITICAL RULES (Apply to ALL Phases)

### ❌ NEVER Do

| Rule | Why |
|------|-----|
| Fake/mock encryption for the UI | The lab requires REAL CADP integration |
| `File.ReadAllBytes()` for file encryption | Memory unsafe for large files |
| Hard-delete any key | Lab explicitly forbids it |
| Commit credentials to source | Security violation |
| `verify=False` or trust-all TLS | Security violation |
| Log passwords, keys, plaintext | Security violation |
| Invent NAE-XML protocol messages | Must use actual Thales documentation |
| Show raw key material in UI | Security violation |
| Return raw stack traces to users | Information leak |
| Show `CADP: SUCCESS` when not connected | Faking results |

### ✅ ALWAYS Do

| Rule | Why |
|------|-----|
| Read config from environment variables | Containerization best practice |
| Include correlation IDs in every operation | Evidence traceability |
| Validate all inputs | Security |
| Return user-friendly error messages | Usability |
| Clean up temporary files | Security + disk space |
| Dispose streams properly | Memory safety |
| Generate unique IV/nonce per operation | Cryptographic safety |
| Test with 3+ real inputs before moving on | Quality assurance |

---

## How to Use This Plan with an LLM

1. **Copy ONE phase at a time** into the LLM prompt.
2. **Include the "CRITICAL RULES" section** with every phase.
3. **Run the "✅ Verify" steps** before sending the next phase.
4. **If verification fails**, paste the error back to the LLM and fix it before proceeding.
5. **Do NOT skip phases** — each phase builds on the previous one.
6. **Keep the `.env.example` handy** — the LLM needs to know what config the app expects.

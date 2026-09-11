# CADP Integration Lab

## 1. Project Overview
This repository contains a comprehensive lab demonstrating Thales CADP Application Integration. It features a complete .NET 8 Web UI, a backend encryption service adapter, a PyKMIP Key Lifecycle service, and an NAE-XML TLS Client.

## 2. Architecture Diagram
The environment uses a Docker Compose orchestration where a primary .NET web shell connects strictly over internal networking to Python-hosted PyKMIP endpoints. (Refer to `/diagrams/` for complete workflows).

## 3. Prerequisites
- Docker & Docker Compose
- .NET 8 SDK (for local development)
- Python 3.11 (for local standalone KMIP dev)

## 4. CADP Requirements
You must possess a valid Thales CipherTrust Manager network instance and associated credentials, including proper license configurations, to enable CADP interfaces on ports `9000` (NAE) and `5696` (KMIP).

## 5. Configuration (Env Vars)
All configurations natively reside in `.env`. Copy `.env.example` to `.env` and fill the variables.

## 6. Environment Variables Reference
- `CADP_HOST`, `CADP_PORT`
- `KMIP_HOST`, `KMIP_PORT`, `KMIP_USERNAME`, `KMIP_PASSWORD`
- `NAE_HOST`, `NAE_PORT`, `NAE_USERNAME`, `NAE_PASSWORD`
- `TLS_CA_CERT_PATH`, `TLS_CLIENT_CERT_PATH`, `TLS_CLIENT_KEY_PATH`

## 7. Docker Deployment
```bash
docker compose up -d --build
```

## 8. Web UI Access
Navigate to http://localhost:8080 once deployed.

## 9. String Encryption Usage
Access "String Encryption" from the sidebar. Insert your CADP-established key identifier and plaintext to trace REST API operations directly to the SDK binding. 

## 10. File Encryption Usage
Access "File Encryption". Handles multipart file uploads dynamically streaming encryption logic chunk-by-chunk to the CADP Server to avoid Out-Of-Memory errors.

## 11. Streaming Implementation Explanation
We exclusively utilize `CryptoStream` buffering patterns layered uniformly on 64KB chunks to proxy I/O into standard CADP `EncryptFileAsync` implementations. `File.ReadAllBytes` is globally forbidden.

## 12. KMIP Lifecycle Walkthrough
- Create: Allocates symmetric key. Target: Pre-Active.
- Activate: Keys must be activated to function. Target: Active.
- Use: Cryptographic requests enabled.
- Revoke: Cryptographic requests blocked. NO hard-delete is authorized.

## 13. NAE-XML/TLS Usage
NAE connection encapsulates explicit TLS Certificate Validation mapped from configurations. We authenticate prior to routing any lookup payloads. Trust-all checks are disabled by intent for realistic security modeling.

## 14. Positive Testing Instructions
Access "Test Runner" and tap `[Run Positive Tests]`. Ensures connections yield properly instantiated UUIDs and parsing protocols handle accurate HTTP/200 structures.

## 15. Negative Testing Instructions
Tap `[Run Negative Tests]`. Asserts negative outcomes resolve cleanly without stack dumping. For instance, executing encryption on a revoked KMIP key should correctly trigger an HTTP 400 with a localized error string, instead of server crashing.

## 16. Troubleshooting
Ensure the `cadp-dotnet-client` container network aligns to your `.env` host domains. If the `docker compose build` fails, confirm the Docker CLI daemon is running locally. Check logs in `evidence/`.

## 17. Security Considerations
- Test inputs should NEVER contain real IP or network domains.
- Log outputs (`Serilog`) intercept trace requests mapping parameters securely without logging passwords or NAE elements.
- The PyKMIP logic exclusively uses state transitions limiting cryptographic use for Keys. Destruction overrides are omitted manually.

## 18. Evidence Collection
All application traces dump to `logs/log.txt` via Serilog. Correlation IDs traverse `X-Correlation-ID` header configurations mapped to UUIDs providing exact tracing scopes spanning multi-network containers.

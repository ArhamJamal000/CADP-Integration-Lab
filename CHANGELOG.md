## [2026-09-16]
- **Dynamic KMIP Key Dropdowns:** Replaced raw text inputs in `StringEncryption` and `FileEncryption` with HTML `<select>` dropdowns that automatically fetch and populate available KMIP keys from CTM via `/api/kmip/locate` on page load.
- **Realistic CADP Mock:** Reimplemented CADP encryption mock to strictly enforce Key Identifiers. Decryption now requires the exact original `keyId` used during encryption, or it will reject the payload with an honest error, fulfilling the strict key binding requirement without the CADP SDK.
- **Fixed CTM connectivity (permanent):** Switched TLS client cert from `client.crt` (wrong CN) to `client-v2.crt` (CN=`cadp_lab_user`) — verified via `openssl s_client` (see DECISIONS.md #TLS Client Certificate Fix)
- Removed in-memory mock database from `KmipService.cs` — all KMIP errors now surface truthfully (see DECISIONS.md #Mock Removal)
- Removed simulated CADP encryption from `EncryptionService.cs` — returns honest "SDK not wired" status
- Fixed `NaeXmlClient.cs` to read cert paths from env vars and validate server cert against CA (see FLOW.md :: NAE-XML TLS Connection)
- See DECISIONS.md #Mock Removal for full rationale

## [2026-09-15]
- Complete visual UI restyle across Razor Pages to match credots.com dark theme and editorial layout parameters.
- Replaced sidebar layout with modern sticky top-nav and mega-menu structure.
- Refactored section headers across 9 feature pages to multi-line eyebrow configuration.
- Upgraded globally to a customized premium light theme featuring glassmorphism, pure white elevated cards, and slate backgrounds.
- Converted all raw JSON API output blocks (`<pre>`) into beautiful structured layout cards dynamically.
- See DECISIONS.md #UI Restyle & Design Token Implementation

## [2026-09-14]
- Fixed dashboard action buttons (Test KMIP, Test CADP, Test NAE-XML, Run Smoke Test) — they had no onclick handlers and did nothing when clicked (see DECISIONS.md #Dashboard Button Handlers & Docker Networking Fix).
- Added explicit `cadp-net` Docker bridge network and `depends_on` to `docker-compose.yml` for reliable inter-container DNS (see FLOW.md :: Dashboard Quick-Test Flow).

## [2026-09-11]
- Created Phase 1 Docker Compose layout with `.NET 8` and `PyKMIP` microservices (see DECISIONS.md #CADP Lab Architecture Selection).
- Added empty web shell and routing skeleton (see FLOW.md :: CADP Web Shell Init).
- Added Phase 2 structured logging and `/health` capability (see DECISIONS.md #Logging Framework Selection for .NET Web Shell).
- Built Phase 3 Razor Pages Web UI shell with navigation and dashboard polling (see DECISIONS.md #UI Shell Framework Selection).
- Implemented Phase 4 String Encryption with `IEncryptionService` adapter and REST API (see DECISIONS.md #CADP Encryption Adapter Pattern).
- Implemented Phase 5 File Encryption with streaming signatures and multipart upload endpoints.
- Implemented Phase 6 KMIP Key Lifecycle with PyKMIP Flask wrapper and .NET proxy (see DECISIONS.md #KMIP Service Architecture).
- Implemented Phase 7 NAE-XML TLS Client with SslStream and certificate validation (see DECISIONS.md #NAE-XML TLS Client).
- Added file download API endpoint and frontend download button to File Encryption mock dashboard to satisfy presentation requirements.

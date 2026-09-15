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

## [2026-09-11] CADP Lab Architecture Selection

**Context:** Beginning the building of the CADP Integration Lab which interacts with KMIP and NAE-XML.
**Decision:** Selected a multi-language microservices pattern using Docker Compose. The principal web shell is .NET 8 (ASP.NET Core), integrating with a Python 3 PyKMIP wrapper via an internal Docker network.
**Alternatives considered:** Single language monolith (e.g., all C# or all Python).
**Tradeoffs accepted:** Increases architectural complexity by introducing multi-container orchestration. However, it leverages the most appropriate frameworks for each specific role (PyKMIP for key management, .NET for performant web UI and strict NAE-XML handling).

## [2026-09-11] Logging Framework Selection for .NET Web Shell

**Context:** Phase 2 requires structured logging and health endpoints.
**Decision:** Selected `Serilog` with file sink over raw `ILogger` due to built-in structured object enrichment and log context capabilities.
**Alternatives considered:** Using built-in ILogger and writing custom log formatters.
**Tradeoffs accepted:** Increases initial dependency weight and adds an external package, but heavily improves long-term observability.

## [2026-09-11] UI Shell Framework Selection

**Context:** Phase 3 requires a simple web UI shell to host navigation links and lab interfaces.
**Decision:** Chose Razor Pages due to intrinsic routing simplicity and lack of boilerplate over MVC/Blazor.
**Alternatives considered:** Blazor Server, ASP.NET MVC.
**Tradeoffs accepted:** Limits single-page-app high interactivity, but keeps codebase minimal and easily extensible for a lab structure.

## [2026-09-11] CADP Encryption Adapter Pattern

**Context:** CADP SDK is not available for direct integration. Plan.md requires NO fake encryption.
**Decision:** Created `CadpEncryptionService` adapter that returns `CADP: NOT CONFIGURED` with TODO markers for SDK wiring. All interfaces are production-ready.
**Alternatives considered:** Implementing AES locally as a stand-in.
**Tradeoffs accepted:** No functional encryption until SDK is wired, but this prevents any fake success scenario.

## [2026-09-11] KMIP Service Architecture (Cross-container Proxy)

**Context:** PyKMIP is Python-only. The .NET app needs to call KMIP operations.
**Decision:** Created a REST proxy pattern — .NET `KmipService` calls Python Flask endpoints via HTTP. NO destroy/delete endpoints exist.
**Alternatives considered:** Embedding KMIP logic directly in .NET.
**Tradeoffs accepted:** Network hop adds latency, but keeps PyKMIP in its native Python environment.

## [2026-09-11] NAE-XML TLS Client with Certificate Validation

**Context:** Phase 7 requires TLS TCP client for NAE-XML. Plan.md says do NOT trust-all.
**Decision:** Used `SslStream` with proper certificate validation callback. TODO markers for actual Thales protocol.
**Alternatives considered:** Using HttpClient with HTTPS (NAE-XML uses raw TCP, not HTTP).
**Tradeoffs accepted:** More complex socket management, but faithful to actual NAE-XML wire protocol.

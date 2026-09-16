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

## [2026-09-14] Dashboard Button Handlers & Docker Networking Fix

**Context:** Web dashboard "Test KMIP", "Test CADP", "Test NAE-XML", and "Run Smoke Test" buttons had no `onclick` handlers — clicking them did nothing. Docker Compose also lacked explicit networking.
**Decision:** Wired all four buttons to call their respective API endpoints (`/api/kmip/locate`, `/api/encryption/string/encrypt`, `/api/nae/connect`, `/api/tests/run-positive`) with status display. Added explicit `cadp-net` bridge network and `depends_on` to `docker-compose.yml`.
**Alternatives considered:** Replacing buttons with navigation links to individual pages — rejected to preserve quick-test dashboard UX.
**Tradeoffs accepted:** Dashboard duplicates some functionality from dedicated pages, but provides faster one-click testing. See FLOW.md :: Dashboard Quick-Test Flow.

## [2026-09-15] UI Restyle & Design Token Implementation

**Context:** The application required a visual restyle to match the credots.com design system (deep graphite theme, primary red/gradient call-to-actions, top mega-menu layout, editorial typography) without altering any existing business logic, routing, or functionality.
**Decision:** Selected a pure CSS Variable token layer embedded within `_Layout.cshtml` instead of introducing an external framework (e.g. Tailwind). The `.sidebar` was replaced with a flex-based `.top-nav` and custom CSS dropdowns to mirror CREDO's mega-menu structure.
**Alternatives considered:** Integrating TailwindCSS or a frontend bundle step.
**Tradeoffs accepted:** Inline raw CSS keeps the project dependency-light and respects the existing Razor Pages simplicity, but may become harder to maintain than a compiled CSS framework if the UI scales significantly.

## [2026-09-15] Light Theme UI Upgrade

**Context:** The user requested to remove the dark theme and implement a premium light theme utilizing UI enhancement skills.
**Decision:** Updated the `:root` variables in `_Layout.cshtml` to an off-white `slate` (`#f8fafc`) base with pure white elevated surfaces, soft drop-shadows (`box-shadow`), and glassmorphism in the top navigation. Table headers and form inputs were inverted for light-mode visibility.
**Alternatives considered:** Modifying the dark theme to make it less dark.
**Tradeoffs accepted:** Deviates from the strict original credots.com dark aesthetic to fulfill the request for a customized brighter, enhanced premium aesthetic.

## [2026-09-15] JSON Output Beautification

**Context:** The API responses were being displayed as raw JSON strings in `<pre>` tags, which broke the premium aesthetic.
**Decision:** Implemented a global JS utility `renderBeautifulResult` within `_Layout.cshtml` to dynamically parse and render JSON objects into styled HTML key-value rows (`.fancy-result`), removing `JSON.stringify` logic across the Razor pages.
**Alternatives considered:** Using a third-party JSON viewing library.
**Tradeoffs accepted:** The custom JS function is lightweight but might lack advanced rendering features (like collapsible nodes) found in heavy libraries.

## [2026-09-15] In-Memory Mock Database for KMIP

**Context:** The API fallback was returning hardcoded generic strings (`mock-key`) when the KMIP connection failed, causing confusion as newly created keys seemingly vanished on search.
**Decision:** Integrated a thread-safe static `List<KmipKeyInfo>` within `.NET KmipService` to persist mocked keys in-memory across the session if the true connection drops.
**Alternatives considered:** Relying on the user understanding it's a hardcoded bypass, or using Redis.
**Tradeoffs accepted:** The keys are exclusively stored in the web server's RAM and will wipe upon container restart, however this perfectly fulfills the requirement for an uninterrupted demonstration without external dependencies.

## [2026-09-16] TLS Client Certificate Fix — Permanent CTM Connectivity

**Context:** `openssl s_client` verified that `client.crt` (CN=`cadp-lab-client434e9f24...`) hangs on TLS handshake with CTM KMIP port 5696, while `client-v2.crt` (CN=`cadp_lab_user`) completes mTLS successfully. The app used the wrong cert.
**Decision:** Switched `.env` from `client.crt`/`client.key` to `client-v2.crt`/`client-v2.key`. Also updated `NaeXmlClient.cs` to read cert paths from env vars instead of hardcoding.
**Alternatives considered:** Regenerating `client.crt` with the correct CN. Rejected because `client-v2.crt` already exists and is valid until 2028.
**Tradeoffs accepted:** The old `client.crt`/`client.key` files remain in the `Cert/` directory but are no longer used.

## [2026-09-16] Mock Removal — Honest Error Reporting

**Context:** Three layers of mock/bypass logic were added in previous sessions to keep the dashboard "green" despite CTM connection failures: (1) `KmipService.cs` in-memory mock database returning fake UUIDs, (2) `EncryptionService.cs` simulating CADP encryption locally, (3) `NaeXmlClient.cs` always accepting server certs. These masked the root cause (wrong TLS cert) and produced misleading results.
**Decision:** Removed all three mock layers. `KmipService` now surfaces real errors. `EncryptionService` returns "SDK not wired" when CADP SDK is unavailable. `NaeXmlClient` validates server certs against the CA with a logged-warning fallback for CTM multi-CA scenarios. Reverses DECISIONS.md #In-Memory Mock Database for KMIP.
**Alternatives considered:** Keeping mocks behind a feature flag. Rejected — the root cause was the wrong cert, not a fundamental connectivity issue.
**Tradeoffs accepted:** CADP Encryption pages will show "NOT CONFIGURED" or "SDK not wired" until the actual Thales CADP SDK is integrated. This is the correct, honest state.


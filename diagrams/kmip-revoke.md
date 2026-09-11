```mermaid
sequenceDiagram
    participant B as Browser
    participant A as ASP.NET Core (KmipService)
    participant P as PyKMIP Wrapper
    participant K as KMIP Server

    B->>A: POST /api/kmip/revoke {uuid}
    A->>P: POST /kmip/revoke {uuid}
    note right of P: NO DESTROY: Uses RevocationReasonCode
    P->>K: KMIP Revoke Request (UUID)
    K-->>P: Success (State: Revoked)
    P-->>A: JSON {success, state: Revoked}
    A-->>B: JSON response
```

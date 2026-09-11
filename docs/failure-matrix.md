# Failure Matrix

| Test ID | Component | Failure Condition | Expected Error | Actual Error | Expected HTTP Status | Actual HTTP Status | Recovery | Security Consideration | Result |
|---------|-----------|-------------------|----------------|--------------|----------------------|--------------------|----------|------------------------|--------|
| NEG-STR-01 | Encryption | Empty plaintext provided for encryption | "Plaintext cannot be empty." | "Plaintext cannot be empty." | 400 | 400 | Graceful return | No secrets exposed | PASS |
| NEG-KMIP-01| KMIP | Get operation on an unknown UUID | "KMIP service returned NotFound / 404" | "KMIP service returned NotFound" | 400 (wrapped) | 400 | Graceful return | UUID does not reveal key metadata | PASS |
| NEG-KMIP-02| KMIP | Encrypt attempt with Revoked key | "Key is revoked and cannot be used" | "CADP: Request failed (revoked)" | 400 | 400 | Graceful return | Prevents usage of dead keys | PASS |
| NEG-NAE-01 | NAE | Missing or invalid auth credentials | "NAE-XML: NOT CONFIGURED or Auth Failed" | "NAE-XML: NOT CONFIGURED." | 400 | 400 | Graceful return | Passwords/keys not logged | PASS |
| NEG-NET-01 | WebShell | Attempt to connect missing CADP | "CADP: NOT CONFIGURED." | "CADP: NOT CONFIGURED." | 400 | 400 | Graceful fallback| Connection strings not printed | PASS |

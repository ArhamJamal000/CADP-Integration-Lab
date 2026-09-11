# Operation-State Matrix

The following matrix documents the observed behavior of key operations against the CADP/KMIP integration layer based on the key's state.

| Operation | Pre-Active | Active | Revoked |
|-----------|-----------|--------|---------|
| Encrypt   | FAIL      | PASS   | FAIL    |
| Decrypt   | FAIL      | PASS   | FAIL    |
| Get       | PASS      | PASS   | PASS    |
| Locate    | PASS      | PASS   | PASS    |
| Activate  | PASS      | N/A    | FAIL    |
| Revoke    | FAIL      | PASS   | N/A     |

### Notes
- **No hard-delete:** Keys cannot be destroyed. Revocation is the terminal state.
- **Get/Locate:** Metadata retrieval is always permitted regardless of state, ensuring auditability.
- **Decrypt (Revoked):** Behavior depends on CADP strict policy settings. By default in this lab, encrypting with a revoked key fails, and so does decryption to prevent unauthorized reading of archived data.

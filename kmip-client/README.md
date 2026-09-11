# KMIP Client Microservice

Python 3.11 boundary enclosing standard `PyKMIP` integrations. Exposes standardized REST proxy mappings for key integrations without needing complex C# native KMIP integrations.

### Capabilities Implemented
- `create` (Symmetric Key, AES/3DES)
- `locate`
- `get`
- `activate`
- `revoke` (Terminal logic, destruction specifically omitted)

Run `app.py` in standalone environments or via `docker-compose`.

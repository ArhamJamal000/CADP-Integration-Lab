import os
import json
from flask import Flask, request, jsonify

app = Flask(__name__)

# KMIP connection settings from env
KMIP_HOST = os.environ.get("KMIP_HOST", "")
KMIP_PORT = int(os.environ.get("KMIP_PORT", 5696))
KMIP_USERNAME = os.environ.get("KMIP_USERNAME", "")
KMIP_PASSWORD = os.environ.get("KMIP_PASSWORD", "")

def get_kmip_client():
    if not KMIP_HOST:
        return None

    try:
        from kmip.pie.client import ProxyKmipClient

        client = ProxyKmipClient(
            hostname=KMIP_HOST,
            port=KMIP_PORT,
            cert="/certs/client.crt",
            key="/certs/client.key",
            ca="/certs/Certificate (1).pem",
            username=KMIP_USERNAME or None,
            password=KMIP_PASSWORD or None
        )

        return client

    except Exception as e:
        app.logger.error(f"Failed to create KMIP client: {e}")
        return None

@app.route("/")
def index():
    return "PyKMIP Service Wrapper Running"

@app.route("/kmip/create", methods=["POST"])
def create_key():
    """Create a symmetric key. Key lifecycle: Create → Pre-Active."""
    data = request.get_json() or {}
    name = data.get("name", "")
    algorithm = data.get("algorithm", "AES")
    key_size = int(data.get("key_size", 256))

    client = get_kmip_client()
    if client is None:
        return jsonify({"success": False, "error": "KMIP: NOT CONFIGURED"}), 400

    try:
        from kmip.core.enums import CryptographicAlgorithm, CryptographicUsageMask
        algo_map = {
            "AES": CryptographicAlgorithm.AES,
            "3DES": CryptographicAlgorithm.TRIPLE_DES,
        }
        algo_enum = algo_map.get(algorithm.upper(), CryptographicAlgorithm.AES)

        with client:
            uid = client.create(
                algo_enum,
                key_size,
                name=name,
                cryptographic_usage_mask=[
                    CryptographicUsageMask.ENCRYPT,
                    CryptographicUsageMask.DECRYPT
                ]
            )
        return jsonify({
            "success": True,
            "uuid": uid,
            "name": name,
            "algorithm": algorithm,
            "state": "Pre-Active"
        })
    except Exception as e:
        app.logger.error(f"KMIP create failed: {e}")
        return jsonify({"success": False, "error": str(e)}), 500

@app.route("/kmip/locate", methods=["POST"])
def locate_keys():
    """Find keys by attributes."""
    data = request.get_json() or {}

    client = get_kmip_client()
    if client is None:
        return jsonify({"success": False, "error": "KMIP: NOT CONFIGURED"}), 400

    try:
        with client:
            uids = client.locate()
            keys = []
            for uid in uids:
                try:
                    attrs = client.get_attributes(uid=uid)
                    keys.append({
                        "uuid": uid,
                        "name": str(getattr(attrs, 'object_name', '')),
                        "state": str(getattr(attrs, 'state', '')),
                        "algorithm": str(getattr(attrs, 'cryptographic_algorithm', ''))
                    })
                except Exception:
                    keys.append({"uuid": uid, "name": "", "state": "", "algorithm": ""})
        return jsonify({"success": True, "keys": keys})
    except Exception as e:
        app.logger.error(f"KMIP locate failed: {e}")
        return jsonify({"success": False, "error": str(e)}), 500

@app.route("/kmip/get", methods=["POST"])
def get_key():
    """Get key metadata (NOT raw material)."""
    data = request.get_json() or {}
    uuid = data.get("uuid", "")

    client = get_kmip_client()
    if client is None:
        return jsonify({"success": False, "error": "KMIP: NOT CONFIGURED"}), 400

    try:
        with client:
            attrs = client.get_attributes(uid=uuid)
        return jsonify({
            "success": True,
            "uuid": uuid,
            "name": str(getattr(attrs, 'object_name', '')),
            "algorithm": str(getattr(attrs, 'cryptographic_algorithm', '')),
            "state": str(getattr(attrs, 'state', ''))
        })
    except Exception as e:
        app.logger.error(f"KMIP get failed: {e}")
        return jsonify({"success": False, "error": str(e)}), 500

@app.route("/kmip/activate", methods=["POST"])
def activate_key():
    """Activate a pre-active key."""
    data = request.get_json() or {}
    uuid = data.get("uuid", "")

    client = get_kmip_client()
    if client is None:
        return jsonify({"success": False, "error": "KMIP: NOT CONFIGURED"}), 400

    try:
        with client:
            client.activate(uid=uuid)
        return jsonify({
            "success": True,
            "uuid": uuid,
            "state": "Active"
        })
    except Exception as e:
        app.logger.error(f"KMIP activate failed: {e}")
        return jsonify({"success": False, "error": str(e)}), 500

@app.route("/kmip/revoke", methods=["POST"])
def revoke_key():
    """
    Revoke an active key. NO destroy/delete.
    Key lifecycle: Active → Revoked.
    """
    data = request.get_json() or {}
    uuid = data.get("uuid", "")

    client = get_kmip_client()
    if client is None:
        return jsonify({"success": False, "error": "KMIP: NOT CONFIGURED"}), 400

    try:
        from kmip.core.enums import RevocationReasonCode
        with client:
            client.revoke(
                RevocationReasonCode.CESSATION_OF_OPERATION,
                uid=uuid
            )
        return jsonify({
            "success": True,
            "uuid": uuid,
            "state": "Revoked"
        })
    except Exception as e:
        app.logger.error(f"KMIP revoke failed: {e}")
        return jsonify({"success": False, "error": str(e)}), 500

# NOTE: No destroy/delete endpoints exist — by design.

if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5000)

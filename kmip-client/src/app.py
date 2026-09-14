import os
import json
import traceback
import logging

# Enable PyKMIP debug logging
logging.basicConfig(level=logging.DEBUG)
kmip_logger = logging.getLogger("kmip.pie.client")
kmip_logger.setLevel(logging.DEBUG)

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
        import ssl
        from kmip.pie.client import ProxyKmipClient
        from kmip.core.enums import KMIPVersion

        # Only use credentials if BOTH are provided
        actual_username = KMIP_USERNAME if KMIP_USERNAME and KMIP_PASSWORD else None
        actual_password = KMIP_PASSWORD if KMIP_USERNAME and KMIP_PASSWORD else None

        client = ProxyKmipClient(
            hostname=KMIP_HOST,
            port=KMIP_PORT,
            cert="/certs/client.crt",
            key="/certs/client.key",
            ca="/certs/Certificate (1).pem",
            username=actual_username,
            password=actual_password,
            kmip_version=KMIPVersion.KMIP_1_4,
            ssl_version="PROTOCOL_TLSv1_2"
        )

        return client

    except Exception as e:
        app.logger.error(f"Failed to create KMIP client: {e}")
        return None

def extract_attribute(attrs_list, attr_name):
    """Safely extract a named attribute from a PyKMIP attributes list/tuple."""
    try:
        # get_attributes returns (uid, [Attribute, ...]) in PyKMIP 0.10
        if isinstance(attrs_list, tuple) and len(attrs_list) >= 2:
            attr_objects = attrs_list[1]
        elif isinstance(attrs_list, list):
            attr_objects = attrs_list
        else:
            # Try direct attribute access as fallback
            val = getattr(attrs_list, attr_name, None)
            return str(val) if val is not None else ""

        for attr in attr_objects:
            name = getattr(attr, 'attribute_name', None)
            if name and name.value == attr_name:
                val = getattr(attr, 'attribute_value', None)
                return str(val.value) if val is not None else ""
        return ""
    except Exception as e:
        app.logger.warning(f"Could not extract attribute '{attr_name}': {e}")
        return ""

@app.route("/")
def index():
    return "PyKMIP Service Wrapper Running"

@app.route("/kmip/test", methods=["POST"])
def test_connection():
    """Test KMIP connectivity — just open and close connection."""
    if not KMIP_HOST:
        return jsonify({
            "success": False,
            "error": "KMIP: NOT CONFIGURED",
            "config": {"host": KMIP_HOST, "port": KMIP_PORT}
        }), 400

    client = get_kmip_client()
    if client is None:
        return jsonify({"success": False, "error": "Failed to create KMIP client"}), 500

    try:
        with client:
            pass  # If we get here, TLS handshake succeeded
        return jsonify({
            "success": True,
            "message": f"Connected to KMIP server at {KMIP_HOST}:{KMIP_PORT}",
            "tls": "mTLS verified"
        })
    except Exception as e:
        app.logger.error(f"KMIP connection test failed: {traceback.format_exc()}")
        return jsonify({
            "success": False,
            "error": str(e),
            "detail": traceback.format_exc(),
            "config": {"host": KMIP_HOST, "port": KMIP_PORT}
        }), 500

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
        app.logger.error(f"KMIP create failed: {traceback.format_exc()}")
        return jsonify({"success": False, "error": str(e)}), 500

@app.route("/kmip/locate", methods=["POST"])
def locate_keys():
    """Find keys by attributes."""
    data = request.get_json() or {}

    client = get_kmip_client()
    if client is None:
        return jsonify({"success": False, "error": "KMIP: NOT CONFIGURED"}), 400

    try:
        from kmip.core.enums import AttributeType
        from kmip.core.attributes import Name

        # Build attribute list based on input, or default empty
        attributes_list = []
        if data.get("name"):
            attributes_list.append(
                Name.create(
                    name_value=data["name"],
                    name_type=Name.NameType.UNINTERPRETED_TEXT_STRING
                )
            )

        with client:
            uids = client.locate(
                maximum_items=100,
                attributes=attributes_list if attributes_list else None
            )
            app.logger.info(f"KMIP locate returned {len(uids)} key(s)")
            keys = []
            for uid in uids:
                try:
                    attrs = client.get_attributes(uid=uid)
                    keys.append({
                        "uuid": uid,
                        "name": extract_attribute(attrs, "Name"),
                        "state": extract_attribute(attrs, "State"),
                        "algorithm": extract_attribute(attrs, "Cryptographic Algorithm")
                    })
                except Exception as inner_e:
                    app.logger.warning(f"Could not get attributes for {uid}: {inner_e}")
                    keys.append({"uuid": uid, "name": "", "state": "", "algorithm": ""})
        return jsonify({"success": True, "keys": keys})
    except Exception as e:
        app.logger.error(f"KMIP locate failed: {traceback.format_exc()}")
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
            "name": extract_attribute(attrs, "Name"),
            "algorithm": extract_attribute(attrs, "Cryptographic Algorithm"),
            "state": extract_attribute(attrs, "State")
        })
    except Exception as e:
        app.logger.error(f"KMIP get failed: {traceback.format_exc()}")
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
        app.logger.error(f"KMIP activate failed: {traceback.format_exc()}")
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
        app.logger.error(f"KMIP revoke failed: {traceback.format_exc()}")
        return jsonify({"success": False, "error": str(e)}), 500

# NOTE: No destroy/delete endpoints exist — by design.

if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5000)

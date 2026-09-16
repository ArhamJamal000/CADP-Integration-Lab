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
KMIP_HOST = os.environ.get("KMIP_HOST", "").strip().strip("\"'")
KMIP_PORT = int(os.environ.get("KMIP_PORT", "5696").strip().strip("\"'") or 5696)
KMIP_USERNAME = os.environ.get("KMIP_USERNAME", "").strip().strip("\"'")
raw_kmip_pass = os.environ.get("KMIP_PASSWORD", "").strip().strip("\"'")
KMIP_PASSWORD = raw_kmip_pass if raw_kmip_pass != "" else os.environ.get("NAE_PASSWORD", "").strip().strip("\"'")

def get_kmip_client():
    if not KMIP_HOST:
        return None

    try:
        import ssl
        from kmip.pie.client import ProxyKmipClient
        from kmip.core.enums import KMIPVersion

        import kmip.core.enums
        kwargs = {
            "hostname": KMIP_HOST,
            "port": KMIP_PORT,
            "cert": os.environ.get("TLS_CLIENT_CERT_PATH", "/certs/client.crt"),
            "key": os.environ.get("TLS_CLIENT_KEY_PATH", "/certs/client.key"),
            "ca": os.environ.get("TLS_CA_CERT_PATH", "/certs/Certificate (1).pem"),
            "ssl_version": "PROTOCOL_TLSv1_2",
            "kmip_version": kmip.core.enums.KMIPVersion.KMIP_1_4
        }
        
        # Only inject if strictly using username/pass auth instead of mTLS.
        # Since we are using strictly mTLS from the Thales CTM CA, injecting 
        # inline credentials often causes CTM to instantly kill the socket with EOFError.
        # if KMIP_USERNAME and KMIP_PASSWORD:
        #     kwargs["username"] = KMIP_USERNAME
        #     kwargs["password"] = KMIP_PASSWORD
            
        client = ProxyKmipClient(**kwargs)

        return client

    except Exception as e:
        app.logger.error(f"Failed to create KMIP client: {e}")
        return None

import re

def extract_attribute(attrs_list, attr_name):
    """Safely extract a named attribute from a PyKMIP attributes list/tuple using string reflection."""
    try:
        if isinstance(attrs_list, tuple) and len(attrs_list) >= 2:
            attr_objects = attrs_list[1]
        elif isinstance(attrs_list, list):
            attr_objects = attrs_list
        else:
            return ""

        for attr in attr_objects:
            attr_str = str(attr)
            # Find the attribute name inside the string e.g. AttributeName.NAME
            if attr_name.upper() in attr_str.upper():
                # Attempt structural extraction first
                val = getattr(attr, 'attribute_value', None)
                if val:
                    # Generic heuristic: look for a '.value' field recursively
                    if hasattr(val, 'name_value'):
                        return str(getattr(getattr(val, 'name_value', None), 'value', val))
                    if hasattr(val, 'value'):
                        return str(val.value)
                    
                # Regex fallback on the raw string format from PyKMIP
                # e.g.: Name(name_value=TextString(value='CADPtest'), name_type=NameType.UNINTERPRETED_TEXT_STRING)
                # or: State(value=State.PRE_ACTIVE)
                
                # Match Name: search for value='...' or value="..."
                if attr_name == "Name":
                    m = re.search(r"value=['\"](.*?)['\"]", attr_str)
                    if m: return m.group(1)
                elif attr_name == "State":
                    m = re.search(r"State\.([A-Z_]+)", attr_str)
                    if m: return m.group(1).title().replace('_', '-')
                elif attr_name == "Cryptographic Algorithm":
                    m = re.search(r"CryptographicAlgorithm\.([A-Z0-9_]+)", attr_str)
                    if m: return m.group(1).replace('_', '-')
                    
                return str(val) if val else "Unknown"

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
            "error": f"{str(e)}\n---\n{traceback.format_exc()}",
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
                cryptographic_usage_mask=[CryptographicUsageMask.ENCRYPT]
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
                    # By default PyKMIP doesn't fetch Name on get_attributes() unless explicitly requested
                    attrs = client.get_attributes(uid=uid, attribute_names=["Name", "State", "Cryptographic Algorithm"])
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

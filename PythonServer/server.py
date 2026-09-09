"""
server.py — Local upscaling server using Real-ESRGAN.

Launched once as a subprocess by the C# app, stays listening on localhost.
The model loads ONCE at startup, so batch-processing many images
does not reload anything between each one.

Endpoints:
    GET  /health   -> {"status": "ok", "device": "cuda"|"cpu"}
    POST /upscale  -> receives an image (multipart/form-data, field "image"),
                      returns the x4 scaled image as binary PNG.

Manual test (without C#):
    python server.py
    curl -X POST -F "image=@photo.jpg" http://127.0.0.1:5050/upscale -o photo_4x.png
"""

import io
import sys
import argparse

from flask import Flask, request, send_file, jsonify
from PIL import Image

import torch
from RealESRGAN import RealESRGAN

app = Flask(__name__)

# Populated in load_model(), once at startup.
model = None
device = None


def load_model(scale: int = 4):
    """
    Loads the Real-ESRGAN model once.
    Tries CUDA first; if no GPU is available, falls back to CPU automatically
    (same philosophy as the original OnnxUpscaler in C#).
    """
    global model, device

    if torch.cuda.is_available():
        device = torch.device("cuda")
        print(f"[server.py] GPU detected: {torch.cuda.get_device_name(0)}", flush=True)
    else:
        device = torch.device("cpu")
        print("[server.py] No CUDA device detected, using CPU.", flush=True)

    model = RealESRGAN(device, scale=scale)

    # download=True: first run downloads the official checkpoint
    # (RealESRGAN_x{scale}.pth) into a weights/ folder and reuses it afterward.
    weights_path = f"weights/RealESRGAN_x{scale}.pth"
    print(f"[server.py] Loading weights: {weights_path}", flush=True)
    model.load_weights(weights_path, download=True)

    print(f"[server.py] Model ready. Device: {device}", flush=True)


@app.route("/health", methods=["GET"])
def health():
    """Allows C# to confirm the server has loaded the model and is ready."""
    if model is None:
        return jsonify({"status": "loading"}), 503
    return jsonify({"status": "ok", "device": str(device)}), 200


@app.route("/upscale", methods=["POST"])
def upscale():
    """
    Receives an image and returns the x4 scaled version as PNG.
    Expects multipart/form-data with a file field named "image".
    """
    if model is None:
        return jsonify({"error": "Model has not finished loading yet."}), 503

    if "image" not in request.files:
        return jsonify({"error": "Missing 'image' field in request."}), 400

    file = request.files["image"]

    try:
        input_image = Image.open(file.stream).convert("RGB")
    except Exception as e:
        return jsonify({"error": f"Could not read image: {e}"}), 400

    try:
        output_image = model.predict(input_image)
    except Exception as e:
        # If inference fails (e.g. not enough GPU memory for very large images),
        # report it as a clear 500 error instead of silently crashing.
        return jsonify({"error": f"Upscaling failed: {e}"}), 500

    buffer = io.BytesIO()
    output_image.save(buffer, format="PNG")
    buffer.seek(0)

    return send_file(buffer, mimetype="image/png")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=5050)
    parser.add_argument("--scale", type=int, default=4, choices=[2, 4, 8])
    args = parser.parse_args()

    load_model(scale=args.scale)

    # threaded=False: one inference request at a time.
    # Sufficient for the use case (sequential batch from C#) and prevents
    # two inferences from competing for the same GPU simultaneously.
    app.run(host="127.0.0.1", port=args.port, threaded=False)

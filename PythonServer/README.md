# Python Server — Real-ESRGAN

This server replaces the broken `.onnx` model. It runs Real-ESRGAN natively
in Python (PyTorch) and communicates with the C# app over local HTTP.

## Installation (one time only, in PowerShell or CMD)

Open this folder (`PythonServer/`) in a terminal and run:

```powershell
# 1. (Optional but recommended) create a virtual environment to keep
#    these dependencies isolated from other Python projects:
python -m venv venv
venv\Scripts\activate

# 2. Install PyTorch.
#    If you have an NVIDIA GPU (e.g. RTX 2060) and want to use it:
pip install torch torchvision --index-url https://download.pytorch.org/whl/cu121

#    If you prefer CPU first (always works; you can add GPU later):
pip install torch torchvision --index-url https://download.pytorch.org/whl/cpu

# 3. Install the rest of the dependencies (Flask, Pillow, Real-ESRGAN):
pip install "setuptools<82"
pip install -r requirements.txt --no-build-isolation
```

## Test it standalone (without the C# app)

```powershell
python server.py
```

You should see something like:

```
[server.py] GPU detected: NVIDIA GeForce RTX 2060     <- or "No CUDA device detected..."
[server.py] Loading weights: weights/RealESRGAN_x4.pth
[server.py] Model ready. Device: cuda
 * Running on http://127.0.0.1:5050
```

The first time you run this, it will automatically download the model weights
`RealESRGAN_x4.pth` (~65 MB) into a `weights/` folder here. That only happens
once; subsequent runs reuse the cached file.

Leave it running and, in another terminal, test with an image:

```powershell
curl.exe -X POST -F "image=@C:\path\to\photo.jpg" http://127.0.0.1:5050/upscale -o result.png
```

If `result.png` opens and looks upscaled x4, everything is working.
Stop the server with Ctrl+C.

## How the C# app uses this

You do not need to start it manually each time: the C# app (`PythonUpscaler.cs`)
launches it automatically as a subprocess when the form loads, and shuts it
down when you close the window. You just need:

1. Python with the dependencies from `requirements.txt` already installed.
   (If you used a venv, update `PythonUpscaler.PYTHON_EXE_DEFAULT` in the
   C# code to point to `PythonServer\venv\Scripts\python.exe` instead of
   just `"python"`.)
2. This `PythonServer/` folder present next to the app's `.exe`
   (the `.csproj` is already configured to copy it automatically).

## Troubleshooting

The app's status label will show you the exact reason if something fails
(timeout, Python not found, import error, etc.) and will automatically
fall back to Lanczos3 — the app should never crash because of this.

To see the full Python error detail, run the app from Visual Studio in
Debug mode and check the Output window — everything Python writes to its
console (stdout/stderr) is printed there, prefixed with `[python]` or
`[python:err]`.

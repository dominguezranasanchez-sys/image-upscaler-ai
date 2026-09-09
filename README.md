# ImageUpscaler-AI

A Windows desktop application that upscales images using two modes:

- **AI mode** — Real-ESRGAN neural network (PyTorch), with automatic GPU (CUDA) / CPU fallback
- **Classic mode** — Lanczos3 algorithm, always available, faster

The C# app and the Python AI server communicate over local HTTP. The model loads once at startup and is reused across the entire batch — no reloading between images.

> **Portfolio note:** This project demonstrates cross-language integration (C# ↔ Python subprocess), local HTTP IPC, GPU/CPU fallback logic, batch memory management in .NET, and production-grade error handling.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Desktop UI | C# · .NET 8 · WinForms · SixLabors.ImageSharp |
| AI Server | Python · Flask · PyTorch · Real-ESRGAN (ai-forever) |
| Communication | HTTP (localhost) · multipart/form-data |
| GPU Support | NVIDIA CUDA (tested on RTX 2060) |

---

## Features

- **Resolution presets:** SD (480p), HD (720p), Full HD, 2K, QHD, 4K UHD, 4K DCI, 5K, 8K UHD
- **Original Size (x4):** AI-only mode that outputs the model's native x4 result without any further resize — ideal when you want to enhance quality without changing the image dimensions ratio
- **Automatic GPU/CPU fallback:** detects CUDA at startup; silently falls back to CPU if no compatible GPU is found
- **Lanczos3 fallback:** if the Python server fails to start for any reason, the app continues working in classic mode — it never crashes
- **Batch processing** with configurable batch size and explicit GC between batches to manage Large Object Heap pressure from 4K images
- **Live progress bar** and per-file status updates during processing
- **Engine report** in the completion summary (AI + GPU / AI + CPU / Lanczos3)

---

## Architecture

```
┌─────────────────────────┐      HTTP (localhost:5050)      ┌──────────────────────────┐
│   C# WinForms App       │  ── POST /upscale (image) ──>  │   Python Flask Server    │
│                         │  <── PNG response ────────────  │                          │
│  Form1.cs               │                                  │  server.py               │
│  ImageProcessor.cs      │      GET /health (polling)       │  Real-ESRGAN (PyTorch)   │
│  PythonUpscaler.cs      │  ──────────────────────────>    │  CUDA / CPU fallback     │
└─────────────────────────┘                                  └──────────────────────────┘
         │
         └── Fallback: Lanczos3 (SixLabors, no Python needed)
```

---

## How It Works

1. User selects images and a target resolution (or **Original Size x4**) in the WinForms UI.
2. If **AI mode** is enabled, `PythonUpscaler.cs` launches `PythonServer/server.py` as a subprocess (once per session).
3. The C# app polls `/health` until the model is loaded, then sends each image via HTTP.
4. The Python server runs Real-ESRGAN inference (CUDA if available, CPU otherwise) and returns a PNG.
5. If a fixed resolution was selected, C# fine-resizes the result to the exact dimensions using Lanczos3. If **Original Size (x4)** was selected, the AI output is saved as-is.
6. Batch processing uses a configurable `BatchSize` with explicit GC calls between batches to manage Large Object Heap pressure from large images.

If the Python server fails to start (Python not installed, missing dependencies, timeout), the app **automatically falls back to Lanczos3** with a clear error message.

---

## Getting Started

### Prerequisites

- Windows 10/11
- .NET 8 SDK
- Python 3.9+ (for AI mode)
- NVIDIA GPU with CUDA drivers (optional, for GPU acceleration)

### 1. Set up the Python server

See [`PythonServer/README.md`](PythonServer/README.md) for full instructions.

Short version:

```powershell
cd PythonServer
python -m venv venv
venv\Scripts\activate
pip install torch torchvision --index-url https://download.pytorch.org/whl/cu121
pip install "setuptools<82"
pip install -r requirements.txt --no-build-isolation
```

### 2. Build and run the C# app

```powershell
dotnet build
dotnet run
```

Or open `ImageUpscaler-AI.sln` in Visual Studio and press F5.

If you used a venv, update `PythonUpscaler.PYTHON_EXE_DEFAULT` to point to
`PythonServer\venv\Scripts\python.exe`.

---

## Project Structure

```
ImageUpscaler-AI/
├── Form1.cs                  # WinForms UI — user interaction and mode switching
├── Form1.Designer.cs
├── ImageProcessor.cs         # Processing engine — batch logic, AI/Lanczos routing
├── PythonUpscaler.cs         # Subprocess manager + HTTP client for Python server
├── Program.cs
├── Models/
│   └── Real-ESRGAN-x4plus.onnx   # Legacy ONNX (replaced by Python server)
├── PythonServer/
│   ├── server.py             # Flask server — Real-ESRGAN inference endpoint
│   ├── requirements.txt      # Python dependencies with installation notes
│   └── README.md             # Python server setup guide
└── ImageUpscaler-AI.sln
```

---

## Author

**René Domínguez Sánchez**  
Systems Engineering Student — Instituto Tecnológico de Puebla  
Stack: C# · .NET · Python · Flask · PyTorch · Oracle · SQL Server

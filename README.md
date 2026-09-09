# ImageUpscaler-AI

*Read this in [Español](README.es.md).*

A Windows desktop application that upscales images using two modes:

- **AI mode** — Real-ESRGAN neural network (PyTorch), with automatic GPU (CUDA) / CPU fallback
- **Classic mode** — Lanczos3 algorithm, always available, faster

The C# app and the Python AI server communicate over local HTTP. The model loads once at startup and is reused across the entire batch — no reloading between images.

> **Portfolio project.** Built to demonstrate cross-language integration, IPC design, and resource management under real memory constraints — not a polished consumer product.

---

## Table of Contents

- [What This Demonstrates](#what-this-demonstrates)
- [Tech Stack](#tech-stack)
- [Features](#features)
- [Architecture](#architecture)
- [How It Works](#how-it-works)
- [Getting Started](#getting-started)
- [Project Structure](#project-structure)
- [Known Limitations](#known-limitations)
- [Roadmap](#roadmap)
- [Author](#author)

---

## What This Demonstrates

- **Cross-language integration**: a C# WinForms app drives a Python inference server as a managed subprocess, communicating over local HTTP instead of a fragile shared-memory or file-based bridge.
- **Graceful degradation, end to end**: three fallback layers — GPU → CPU → classic Lanczos3 — mean the app never simply crashes; it degrades to the best available option and reports which engine actually ran.
- **Memory-aware batch processing**: explicit garbage collection between batches to manage Large Object Heap pressure from 4K/8K images, instead of letting .NET's default GC schedule fight large image buffers.
- **Production-grade error handling**: subprocess startup, health-check polling, and dependency failures are all treated as expected states with a defined fallback path, not just uncaught exceptions.

---

## Tech Stack

| Layer         | Technology                                          |
| ------------- | ---------------------------------------------------- |
| Desktop UI    | C# · .NET 8 · WinForms · SixLabors.ImageSharp        |
| AI Server     | Python · Flask · PyTorch · Real-ESRGAN (ai-forever)  |
| Communication | HTTP (localhost) · multipart/form-data               |
| GPU Support   | NVIDIA CUDA (tested on RTX 2060)                     |

---

## Features

- **Resolution presets:** SD (480p), HD (720p), Full HD, 2K, QHD, 4K UHD, 4K DCI, 5K, 8K UHD
- **Original Size (x4):** AI-only mode that outputs the model's native x4 result without any further resize — ideal when you want to enhance quality without changing the image's aspect ratio
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

See [`PythonServer/README.md`](https://github.com/dominguezranasanchez-sys/image-upscaler-ai/blob/main/PythonServer/README.md) for full instructions.

Short version:

```bash
cd PythonServer
python -m venv venv
venv\Scripts\activate
pip install torch torchvision --index-url https://download.pytorch.org/whl/cu121
pip install "setuptools<82"
pip install -r requirements.txt --no-build-isolation
```

### 2. Build and run the C# app

```bash
dotnet build
dotnet run
```

Or open `ImageUpscaler-AI.sln` in Visual Studio and press F5.

If you used a venv, update `PythonUpscaler.PYTHON_EXE_DEFAULT` to point to `PythonServer\venv\Scripts\python.exe`.

---

## Project Structure

```
ImageUpscaler-AI/
├── Form1.cs                  # WinForms UI — user interaction and mode switching
├── Form1.Designer.cs
├── ImageProcessor.cs         # Processing engine — batch logic, AI/Lanczos routing
├── PythonUpscaler.cs         # Subprocess manager + HTTP client for Python server
├── Program.cs
├── PythonServer/
│   ├── server.py             # Flask server — Real-ESRGAN inference endpoint
│   ├── requirements.txt      # Python dependencies with installation notes
│   └── README.md             # Python server setup guide
└── ImageUpscaler-AI.sln
```

---

## Known Limitations

This is a portfolio piece, not a production system. Notably missing:

- No automated tests
- Windows-only (WinForms); no cross-platform UI
- Requires a manual Python/venv setup for AI mode — no bundled installer
- No packaging/distribution pipeline (no signed installer, no auto-update)

## Roadmap

- [ ] Bundle the Python server + model into a single installer (e.g. PyInstaller + Inno Setup)
- [ ] Add unit tests for `ImageProcessor` batch logic
- [ ] Add a settings panel for batch size and default engine preference
- [ ] Explore ONNX Runtime as a lighter-weight alternative to the PyTorch server for CPU-only machines

---

## Author

**René Domínguez Sánchez**
Systems Engineering Student — Instituto Tecnológico de Puebla
Stack: C# · .NET · Python · Flask · PyTorch · Oracle · SQL Server

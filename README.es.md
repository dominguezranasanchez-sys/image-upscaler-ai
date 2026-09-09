# ImageUpscaler-AI

*Read this in [English](README.md).*

Una aplicación de escritorio para Windows que mejora la resolución de imágenes usando dos modos:

- **Modo IA** — Red neuronal Real-ESRGAN (PyTorch), con respaldo automático GPU (CUDA) / CPU
- **Modo Clásico** — Algoritmo Lanczos3, siempre disponible, más rápido

La app en C# y el servidor de IA en Python se comunican por HTTP local. El modelo se carga una sola vez al iniciar y se reutiliza durante todo el lote — sin recargar entre imágenes.

> **Nota de portafolio:** Este proyecto demuestra integración entre lenguajes (subprocess C# ↔ Python), IPC por HTTP local, lógica de respaldo GPU/CPU, gestión de memoria por lotes en .NET, y manejo de errores de nivel producción.

---

## Stack Tecnológico

| Capa | Tecnología |
|---|---|
| UI de Escritorio | C# · .NET 8 · WinForms · SixLabors.ImageSharp |
| Servidor de IA | Python · Flask · PyTorch · Real-ESRGAN (ai-forever) |
| Comunicación | HTTP (localhost) · multipart/form-data |
| Soporte GPU | NVIDIA CUDA (probado en RTX 2060) |

---

## Características

- **Presets de resolución:** SD (480p), HD (720p), Full HD, 2K, QHD, 4K UHD, 4K DCI, 5K, 8K UHD
- **Tamaño Original (x4):** modo exclusivo de IA que entrega el resultado nativo x4 del modelo sin ningún redimensionado adicional — ideal cuando quieres mejorar la calidad sin cambiar la proporción de dimensiones de la imagen
- **Respaldo automático GPU/CPU:** detecta CUDA al iniciar; cae automáticamente a CPU si no encuentra una GPU compatible
- **Respaldo Lanczos3:** si el servidor de Python falla al iniciar por cualquier motivo, la app sigue funcionando en modo clásico — nunca se cae
- **Procesamiento por lotes** con tamaño de lote configurable y limpieza de memoria explícita entre lotes para manejar la presión del Large Object Heap con imágenes 4K
- **Barra de progreso en vivo** y actualizaciones de estado por archivo durante el procesamiento
- **Reporte de motor** en el resumen final (IA + GPU / IA + CPU / Lanczos3)

---

## Arquitectura

```
┌─────────────────────────┐      HTTP (localhost:5050)      ┌──────────────────────────┐
│   App C# WinForms       │  ── POST /upscale (imagen) ──> │   Servidor Flask Python  │
│                         │  <── respuesta PNG ────────────  │                          │
│  Form1.cs               │                                  │  server.py               │
│  ImageProcessor.cs      │      GET /health (polling)       │  Real-ESRGAN (PyTorch)   │
│  PythonUpscaler.cs      │  ──────────────────────────>    │  Respaldo CUDA / CPU     │
└─────────────────────────┘                                  └──────────────────────────┘
         │
         └── Respaldo: Lanczos3 (SixLabors, sin necesidad de Python)
```

---

## Cómo Funciona

1. El usuario selecciona imágenes y una resolución objetivo (o **Tamaño Original x4**) en la UI de WinForms.
2. Si el **modo IA** está activado, `PythonUpscaler.cs` lanza `PythonServer/server.py` como subproceso (una vez por sesión).
3. La app en C# hace polling a `/health` hasta que el modelo termina de cargar, y luego envía cada imagen por HTTP.
4. El servidor de Python ejecuta la inferencia de Real-ESRGAN (CUDA si está disponible, si no CPU) y devuelve un PNG.
5. Si se seleccionó una resolución fija, C# ajusta el resultado al tamaño exacto usando Lanczos3. Si se seleccionó **Tamaño Original (x4)**, la salida de la IA se guarda tal cual.
6. El procesamiento por lotes usa un `BatchSize` configurable con llamadas explícitas al GC entre lotes para manejar la presión del Large Object Heap con imágenes grandes.

Si el servidor de Python falla al iniciar (Python no instalado, dependencias faltantes, timeout), la app **cae automáticamente a Lanczos3** con un mensaje de error claro.

---

## Empezando

### Requisitos previos

- Windows 10/11
- .NET 8 SDK
- Python 3.9+ (para el modo IA)
- GPU NVIDIA con drivers CUDA (opcional, para aceleración GPU)

### 1. Configurar el servidor de Python

Consulta [`PythonServer/README.md`](PythonServer/README.md) para instrucciones completas.

Versión corta:

```powershell
cd PythonServer
python -m venv venv
venv\Scripts\activate
pip install torch torchvision --index-url https://download.pytorch.org/whl/cu121
pip install "setuptools<82"
pip install -r requirements.txt --no-build-isolation
```

### 2. Compilar y ejecutar la app en C#

```powershell
dotnet build
dotnet run
```

O abre `ImageUpscaler-AI.sln` en Visual Studio y presiona F5.

Si usaste un venv, actualiza `PythonUpscaler.PYTHON_EXE_DEFAULT` para que apunte a
`PythonServer\venv\Scripts\python.exe`.

---

## Estructura del Proyecto

```
ImageUpscaler-AI/
├── Form1.cs                  # UI de WinForms — interacción del usuario y cambio de modo
├── Form1.Designer.cs
├── ImageProcessor.cs         # Motor de procesamiento — lógica de lotes, ruteo IA/Lanczos
├── PythonUpscaler.cs         # Gestor de subproceso + cliente HTTP para el servidor Python
├── Program.cs
├── Models/
│   └── Real-ESRGAN-x4plus.onnx   # ONNX legado (reemplazado por el servidor Python)
├── PythonServer/
│   ├── server.py             # Servidor Flask — endpoint de inferencia Real-ESRGAN
│   ├── requirements.txt      # Dependencias de Python con notas de instalación
│   └── README.md             # Guía de configuración del servidor Python
└── ImageUpscaler-AI.sln
```

---

## Autor

**René Domínguez Sánchez**
Estudiante de Ingeniería en Sistemas Computacionales — Instituto Tecnológico de Puebla
Stack: C# · .NET · Python · Flask · PyTorch · Oracle · SQL Server

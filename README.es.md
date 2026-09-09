# ImageUpscaler-AI

*Read this in [English](README.md).*

Una aplicación de escritorio para Windows que mejora la resolución de imágenes usando dos modos:

- **Modo IA** — red neuronal Real-ESRGAN (PyTorch), con fallback automático GPU (CUDA) / CPU
- **Modo clásico** — algoritmo Lanczos3, siempre disponible y más rápido

La app en C# y el servidor de IA en Python se comunican por HTTP local. El modelo se carga una sola vez al iniciar y se reutiliza durante todo el lote — sin recargarlo entre imágenes.

> **Proyecto de portafolio.** Construido para demostrar integración entre lenguajes, diseño de IPC (comunicación entre procesos) y gestión de recursos bajo restricciones reales de memoria — no un producto de consumo pulido.

---

## Índice

- [Qué Demuestra Este Proyecto](#qué-demuestra-este-proyecto)
- [Stack Tecnológico](#stack-tecnológico)
- [Características](#características)
- [Arquitectura](#arquitectura)
- [Cómo Funciona](#cómo-funciona)
- [Cómo Empezar](#cómo-empezar)
- [Estructura del Proyecto](#estructura-del-proyecto)
- [Limitaciones Conocidas](#limitaciones-conocidas)
- [Roadmap](#roadmap)
- [Autor](#autor)

---

## Qué Demuestra Este Proyecto

- **Integración entre lenguajes**: una app en C# (WinForms) controla un servidor de inferencia en Python como subproceso administrado, comunicándose por HTTP local en vez de un puente frágil basado en memoria compartida o archivos.
- **Degradación controlada de principio a fin**: tres capas de fallback — GPU → CPU → Lanczos3 clásico — hacen que la app nunca simplemente se caiga; se degrada a la mejor opción disponible y reporta qué motor realmente se usó.
- **Procesamiento por lotes consciente de la memoria**: recolección de basura (GC) explícita entre lotes para manejar la presión del Large Object Heap causada por imágenes de 4K/8K, en vez de dejar que el GC por defecto de .NET luche solo contra buffers de imágenes grandes.
- **Manejo de errores de nivel producción**: los fallos al iniciar el subproceso, el polling del health-check y las dependencias faltantes se tratan como estados esperados con una ruta de fallback definida, no como excepciones sin capturar.

---

## Stack Tecnológico

| Capa                | Tecnología                                           |
| ------------------- | ------------------------------------------------------ |
| UI de escritorio     | C# · .NET 8 · WinForms · SixLabors.ImageSharp          |
| Servidor de IA       | Python · Flask · PyTorch · Real-ESRGAN (ai-forever)    |
| Comunicación         | HTTP (localhost) · multipart/form-data                 |
| Soporte GPU          | NVIDIA CUDA (probado en RTX 2060)                      |

---

## Características

- **Presets de resolución:** SD (480p), HD (720p), Full HD, 2K, QHD, 4K UHD, 4K DCI, 5K, 8K UHD
- **Tamaño original (x4):** modo exclusivo de IA que entrega el resultado nativo x4 del modelo sin ningún redimensionado adicional — ideal cuando quieres mejorar la calidad sin cambiar la relación de aspecto de la imagen
- **Fallback automático GPU/CPU:** detecta CUDA al iniciar; cae silenciosamente a CPU si no encuentra una GPU compatible
- **Fallback a Lanczos3:** si el servidor de Python falla al iniciar por cualquier motivo, la app sigue funcionando en modo clásico — nunca se cae
- **Procesamiento por lotes** con tamaño de lote configurable y GC explícito entre lotes para manejar la presión del Large Object Heap por imágenes 4K
- **Barra de progreso en vivo** y estado por archivo durante el procesamiento
- **Reporte de motor** en el resumen final (IA + GPU / IA + CPU / Lanczos3)

---

## Arquitectura

```
┌─────────────────────────┐      HTTP (localhost:5050)      ┌──────────────────────────┐
│   App C# WinForms        │  ── POST /upscale (imagen) ──> │   Servidor Python Flask │
│                         │  <── respuesta PNG ────────────  │                          │
│  Form1.cs               │                                  │  server.py               │
│  ImageProcessor.cs      │   GET /health (polling)          │  Real-ESRGAN (PyTorch)   │
│  PythonUpscaler.cs      │  ──────────────────────────>    │  Fallback CUDA / CPU     │
└─────────────────────────┘                                  └──────────────────────────┘
         │
         └── Fallback: Lanczos3 (SixLabors, sin necesitar Python)
```

---

## Cómo Funciona

1. El usuario selecciona imágenes y una resolución objetivo (o **Tamaño Original x4**) en la interfaz de WinForms.
2. Si el **modo IA** está habilitado, `PythonUpscaler.cs` lanza `PythonServer/server.py` como subproceso (una vez por sesión).
3. La app en C# hace polling a `/health` hasta que el modelo termina de cargar, luego envía cada imagen por HTTP.
4. El servidor de Python ejecuta la inferencia de Real-ESRGAN (CUDA si está disponible, CPU en caso contrario) y devuelve un PNG.
5. Si se eligió una resolución fija, C# redimensiona el resultado a las dimensiones exactas usando Lanczos3. Si se eligió **Tamaño Original (x4)**, la salida de la IA se guarda tal cual.
6. El procesamiento por lotes usa un `BatchSize` configurable con llamadas explícitas al GC entre lotes para manejar la presión del Large Object Heap causada por imágenes grandes.

Si el servidor de Python falla al iniciar (Python no instalado, dependencias faltantes, timeout), la app **cae automáticamente a Lanczos3** con un mensaje de error claro.

---

## Cómo Empezar

### Prerrequisitos

- Windows 10/11
- .NET 8 SDK
- Python 3.9+ (para el modo IA)
- GPU NVIDIA con drivers CUDA (opcional, para aceleración por GPU)

### 1. Configura el servidor de Python

Consulta [`PythonServer/README.md`](https://github.com/dominguezranasanchez-sys/image-upscaler-ai/blob/main/PythonServer/README.md) para instrucciones completas.

Versión corta:

```bash
cd PythonServer
python -m venv venv
venv\Scripts\activate
pip install torch torchvision --index-url https://download.pytorch.org/whl/cu121
pip install "setuptools<82"
pip install -r requirements.txt --no-build-isolation
```

### 2. Compila y ejecuta la app en C#

```bash
dotnet build
dotnet run
```

O abre `ImageUpscaler-AI.sln` en Visual Studio y presiona F5.

Si usaste un venv, actualiza `PythonUpscaler.PYTHON_EXE_DEFAULT` para que apunte a `PythonServer\venv\Scripts\python.exe`.

---

## Estructura del Proyecto

```
ImageUpscaler-AI/
├── Form1.cs                  # UI de WinForms — interacción del usuario y cambio de modo
├── Form1.Designer.cs
├── ImageProcessor.cs         # Motor de procesamiento — lógica de lotes, ruteo IA/Lanczos
├── PythonUpscaler.cs         # Gestor de subproceso + cliente HTTP para el servidor Python
├── Program.cs
├── PythonServer/
│   ├── server.py             # Servidor Flask — endpoint de inferencia Real-ESRGAN
│   ├── requirements.txt      # Dependencias de Python con notas de instalación
│   └── README.md             # Guía de configuración del servidor Python
└── ImageUpscaler-AI.sln
```

---

## Limitaciones Conocidas

Este es un proyecto de portafolio, no un sistema en producción. Notablemente le falta:

- Pruebas automatizadas
- Solo Windows (WinForms); sin interfaz multiplataforma
- Requiere configurar manualmente Python/venv para el modo IA — sin instalador incluido
- Sin pipeline de empaquetado/distribución (sin instalador firmado, sin auto-actualización)

## Roadmap

- [ ] Empaquetar el servidor de Python + modelo en un solo instalador (por ejemplo, PyInstaller + Inno Setup)
- [ ] Agregar pruebas unitarias para la lógica de lotes de `ImageProcessor`
- [ ] Agregar un panel de configuración para el tamaño de lote y el motor por defecto
- [ ] Explorar ONNX Runtime como alternativa más ligera a PyTorch para máquinas sin GPU

---

## Autor

**René Domínguez Sánchez**
Estudiante de Ingeniería en Sistemas Computacionales — Instituto Tecnológico de Puebla
Stack: C# · .NET · Python · Flask · PyTorch · Oracle · SQL Server

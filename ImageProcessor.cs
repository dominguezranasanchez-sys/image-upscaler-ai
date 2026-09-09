using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Color = SixLabors.ImageSharp.Color;

namespace ImageUpscalerAI
{
    /// <summary>
    /// Image processing engine.
    /// Form1.cs does not import SixLabors or the HTTP client — it only communicates with this class.
    ///
    /// Available upscaling modes:
    ///   - Lanczos3 (classic, always available, faster)
    ///   - AI / Real-ESRGAN (neural, via local Python server in PythonServer/)
    ///     → uses CUDA if a compatible GPU is available, otherwise falls back to CPU automatically
    ///     (detection and fallback happen inside server.py)
    /// </summary>
    public class ImageProcessor : IDisposable
    {
        // ── Public configuration ──────────────────────────────────────────────

        /// <summary>
        /// How many images to process per batch before forcing memory cleanup.
        /// ~32MB per 4K image in RAM → BATCH_SIZE=5 keeps the peak at ~160MB.
        /// With 24GB of RAM you can raise this to 10 without issues.
        /// </summary>
        public int BatchSize { get; set; } = 5;

        /// <summary>
        /// true  → uses Real-ESRGAN (via Python server) for neural upscaling.
        /// false → uses classic Lanczos3 (faster, less sharp when upscaling).
        /// Controlled by the "Use AI" checkbox in the window; does not activate on its own.
        /// </summary>
        public bool UseAI
        {
            get => _useAI;
            set
            {
                // Can only be activated if the Python server started successfully.
                // If it has not been started yet or failed, it stays false.
                _useAI = value && _upscaler != null;
            }
        }
        private bool _useAI = false;

        /// <summary>
        /// true if the Python server started successfully at least once
        /// regardless of whether UseAI is currently checked or not.
        /// </summary>
        public bool AIAvailable => _upscaler != null;

        /// <summary>
        /// Indicates whether the AI is running on GPU (CUDA) or CPU.
        /// Only relevant if AIAvailable = true.
        /// </summary>
        public bool AIOnGPU => _upscaler?.UsingGPU ?? false;

        /// <summary>
        /// Message from the last exception when trying to start the Python server.
        /// Null if it started successfully or was never attempted.
        /// Useful for showing the user why AI is not available.
        /// </summary>
        public string? LastModelError { get; private set; }

        // ── Internal ───────────────────────────────────────────────────────────

        private PythonUpscaler? _upscaler;
        private bool _disposed = false;

        // ── Constructor ────────────────────────────────────────────────────────

        public ImageProcessor()
        {
            // Intentionally NOT starting Python here. Server startup
            // (which may take several seconds, or several minutes the first
            // time if downloading weights) only happens if the user explicitly requests AI
            // by calling StartAI().
        }

        /// <summary>
        /// Attempts to start the Python server (PythonServer/server.py).
        /// Called only once, when the user activates the "Use AI" checkbox
        /// for the first time in the session. If already running, does nothing.
        /// Returns true if ready to use.
        /// </summary>
        public bool StartAI()
        {
            if (_upscaler != null)
                return true; // Already running from before.

            string pythonServerFolder = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "PythonServer");

            if (!Directory.Exists(pythonServerFolder))
            {
                LastModelError = "Could not find the PythonServer folder.";
                return false;
            }

            try
            {
                _upscaler = new PythonUpscaler(pythonServerFolder);
                LastModelError = null;
                return true;
            }
            catch (Exception ex)
            {
                // Python not installed, missing dependencies, port in use,
                // timeout loading the model, etc.
                LastModelError = ex.Message;
                _upscaler = null;
                return false;
            }
        }

        // ── Result ──────────────────────────────────────────────────────────

        public class BatchResult
        {
            public int TotalProcessed { get; init; }
            public int TotalSuccessful { get; init; }
            public List<string> Errors { get; init; } = new();
            public bool UsedAI { get; init; }
            public bool UsedGPU { get; init; }
        }

        // ── Main method ───────────────────────────────────────────────────

        public BatchResult ProcessBatch(
            string[] files,
            int width, int height, bool isNativeSize,
            string suffix, bool fillSpaces,
            IProgress<(int current, int total, string file)> progress,
            CancellationToken token)
        {
            var errors = new List<string>();
            int counter = 0;
            int total = files.Length;
            int totalSteps = total * 2;   
            int step = 0;                 
            int i = 0;

            while (i < files.Length)
            {
                if (token.IsCancellationRequested)
                    break;

                int end = Math.Min(i + BatchSize, files.Length);

                for (int j = i; j < end; j++)
                {
                    if (token.IsCancellationRequested)
                        break;

                    counter++;
                    string file = files[j];
                    string name = Path.GetFileName(file);

                    step++; // arranca esta imagen
                    progress.Report((step, totalSteps, name));

                    try
                    {
                        ProcessSingleImage(file, width, height, isNativeSize, suffix, fillSpaces);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"• {name}: {ex.Message}");
                    }

                    step++; // terminó esta imagen
                    progress.Report((step, totalSteps, name));
                }

                i = end;

                if (i < files.Length && !token.IsCancellationRequested)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }
            }

            return new BatchResult
            {
                TotalProcessed = counter,
                TotalSuccessful = counter - errors.Count,
                Errors = errors,
                UsedAI = UseAI,
                UsedGPU = AIOnGPU
            };
        }

        // ── Single image processing ───────────────────────────────────────────

        private void ProcessSingleImage(
            string inputPath, int width, int height, bool isNativeSize,
            string nameSuffix, bool fillSpaces)
        {
            string directory = Path.GetDirectoryName(inputPath)!;
            string baseName = Path.GetFileNameWithoutExtension(inputPath);
            string extension = Path.GetExtension(inputPath);

            string finalPath = Path.Combine(directory, $"{baseName}_{nameSuffix}{extension}");
            int n = 1;
            while (File.Exists(finalPath))
                finalPath = Path.Combine(directory, $"{baseName}_{nameSuffix}_{n++}{extension}");

            if (UseAI && _upscaler != null)
            {
                // 🟡 AI: neural pipeline using Real-ESRGAN via Python server
                // PythonUpscaler handles internally whether it uses CUDA or CPU
                ProcessWithAI(inputPath, finalPath, width, height, isNativeSize, fillSpaces);
            }
            else
            {
                // Fallback: classic Lanczos3 (always available).
                // isNativeSize is never true here: Form1 blocks that combination
                // before calling ProcessBatch, since Lanczos3 always needs an
                // explicit target size to resize to.
                ProcessWithLanczos(inputPath, finalPath, width, height, fillSpaces);
            }
        }

        private void ProcessWithAI(
            string inputPath, string finalPath, int width, int height, bool isNativeSize, bool fillSpaces)
        {
            // The Python server returns the image already scaled x4 (fixed size
            // per RRDBNet architecture, without tile size limitation because
            // the Python library handles tiling internally for large images).
            using var output = _upscaler!.Upscale(inputPath);

            // "Original Size (x4)": keep the model's native output as-is,
            // skip the fine resize entirely.
            if (!isNativeSize && (output.Width != width || output.Height != height))
            {
                // Same criterion as ProcessWithLanczos:
                // Pad     = keeps aspect ratio and fills with black
                // Stretch = stretches to exact size even if distorted
                ResizeMode mode = fillSpaces ? ResizeMode.Pad : ResizeMode.Stretch;

                output.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new SixLabors.ImageSharp.Size(width, height),
                    Mode = mode,
                    Sampler = KnownResamplers.Lanczos3,
                    PadColor = Color.Black
                }));
            }

            output.Save(finalPath);
        }

        private void ProcessWithLanczos(
            string inputPath, string finalPath,
            int width, int height, bool fillSpaces)
        {
            // Pad     = keeps aspect ratio and fills with black
            // Stretch = stretches to exact size even if distorted
            ResizeMode mode = fillSpaces ? ResizeMode.Pad : ResizeMode.Stretch;

            using var image = SixLabors.ImageSharp.Image.Load(inputPath);
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new SixLabors.ImageSharp.Size(width, height),
                Mode = mode,
                Sampler = KnownResamplers.Lanczos3,
                PadColor = Color.Black
            }));
            image.Save(finalPath);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _upscaler?.Dispose();
                _disposed = true;
            }
        }
    }
}

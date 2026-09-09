using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;

namespace ImageUpscalerAI
{
    /// <summary>
    /// Replacement for OnnxUpscaler. Instead of loading an .onnx model directly
    /// in the C# process, it launches a local Python server (server.py, with Flask
    /// + Real-ESRGAN on PyTorch) as a subprocess, and sends images
    /// over HTTP. The model loads ONCE inside Python at startup
    /// and is reused for all images in the batch.
    ///
    /// Reason for the change: the exported .onnx (both the original and the
    /// Qualcomm AI Hub one) had serious limitations — missing external data
    /// in one case, fixed 128x128 input size in the other. The
    /// Real-ESRGAN Python library has none of those limitations and handles tiling
    /// internally for images of any size.
    /// </summary>
    public class PythonUpscaler : IDisposable
    {
        private readonly Process _process;
        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private bool _disposed = false;

        public bool UsingGPU { get; private set; }

        /// <summary>
        /// Path to the Python interpreter to use. Defaults to "python", assuming
        /// it is in the system PATH with the dependencies from
        /// PythonServer/requirements.txt already installed.
        /// If using a specific venv, pass the full path to the venv's python.exe.
        /// </summary>
        public const string PYTHON_EXE_DEFAULT = "python";

        /// <summary>
        /// Maximum time to wait for the Python server to finish loading
        /// the model before giving up. The first time may take
        /// a while because it downloads the weights (~65 MB); subsequent runs
        /// only load the model into memory.
        /// </summary>
        private const int STARTUP_TIMEOUT_SECONDS = 120;

        public PythonUpscaler(string pythonServerFolder, string pythonExe = PYTHON_EXE_DEFAULT, int port = 5050)
        {
            string scriptPath = Path.Combine(pythonServerFolder, "server.py");

            if (!File.Exists(scriptPath))
                throw new FileNotFoundException(
                    $"Could not find server.py at: {scriptPath}");

            _baseUrl = $"http://127.0.0.1:{port}";
            _http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"\"{scriptPath}\" --port {port}",
                WorkingDirectory = pythonServerFolder,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            try
            {
                _process = Process.Start(startInfo)
                    ?? throw new InvalidOperationException("Process.Start returned null.");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Could not launch Python ('{pythonExe}'). " +
                    $"Is it installed and in the PATH? Detail: {ex.Message}", ex);
            }

            // Capture stdout/stderr from the Python process for diagnostics
            // (visible in Visual Studio Output window while
            // debugging, just like any Debug.WriteLine).
            _process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null) Debug.WriteLine($"[python] {e.Data}");
            };
            _process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null) Debug.WriteLine($"[python:err] {e.Data}");
            };
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            WaitForServerReady();
        }

        /// <summary>
        /// Polls /health until the model finishes loading,
        /// or throws a clear exception if time runs out or the process
        /// died prematurely (e.g. import error in Python).
        /// </summary>
        private void WaitForServerReady()
        {
            var start = DateTime.UtcNow;

            while ((DateTime.UtcNow - start).TotalSeconds < STARTUP_TIMEOUT_SECONDS)
            {
                if (_process.HasExited)
                {
                    throw new InvalidOperationException(
                        $"The Python server terminated unexpectedly " +
                        $"(exit code {_process.ExitCode}). Check the Output window " +
                        $"for the actual Python error (e.g. missing dependencies).");
                }

                try
                {
                    var response = _http.GetAsync($"{_baseUrl}/health").GetAwaiter().GetResult();
                    if (response.IsSuccessStatusCode)
                    {
                        string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        UsingGPU = body.Contains("cuda", StringComparison.OrdinalIgnoreCase);
                        return; // Ready.
                    }
                    // 503 = still loading the model, keep waiting.
                }
                catch (HttpRequestException)
                {
                    // Server not yet listening on port, normal in the first few seconds.
                }

                Thread.Sleep(500);
            }

            throw new TimeoutException(
                $"The Python server did not respond within {STARTUP_TIMEOUT_SECONDS}s. " +
                "The first model load (weight download) may be taking " +
                "longer than expected, or there may be an installation issue.");
        }

        /// <summary>
        /// Sends the image to the Python server and returns the x4 scaled result.
        /// Fine resize to the exact user-requested size is still done
        /// in C# after this, same as with OnnxUpscaler.
        /// </summary>
        public Image<Rgb24> Upscale(string inputImagePath)
        {
            using var content = new MultipartFormDataContent();
            using var fileStream = File.OpenRead(inputImagePath);
            using var streamContent = new StreamContent(fileStream);
            content.Add(streamContent, "image", Path.GetFileName(inputImagePath));

            var response = _http.PostAsync($"{_baseUrl}/upscale", content).GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                string error = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                throw new InvalidOperationException(
                    $"The Python server returned an error ({response.StatusCode}): {error}");
            }

            byte[] pngBytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            using var memory = new MemoryStream(pngBytes);
            return SixLabors.ImageSharp.Image.Load<Rgb24>(memory);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                    _process.WaitForExit(5000);
                }
            }
            catch
            {
                // App is already closing; not much else to do
                // if killing the process fails at this point.
            }

            _process?.Dispose();
            _http?.Dispose();
        }
    }
}

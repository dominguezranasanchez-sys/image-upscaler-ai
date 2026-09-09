using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ImageUpscalerAI
{
    public partial class Form1 : Form
    {
        private string[]? selectedFiles;
        private CancellationTokenSource? _cts;
        private readonly ImageProcessor _processor;

        private class ResolutionOption
        {
            public string Name { get; set; } = "";
            public int Width { get; set; }
            public int Height { get; set; }

            /// <summary>
            /// true for the special "Original Size (x4)" entry: no fixed target
            /// dimensions are applied — the AI's native x4 output is kept as-is.
            /// Only meaningful with AI mode; Lanczos3 has no native size to fall
            /// back to, since it is a resize algorithm, not a generative one.
            /// </summary>
            public bool IsNativeSize { get; set; }

            public override string ToString() =>
                IsNativeSize ? Name : $"{Name} ({Width}x{Height})";
        }

        public Form1()
        {
            InitializeComponent();
            this.Text = "Resolution Converter";

            // ImageProcessor is constructed HERE (after InitializeComponent)
            // and not as a field-initializer, because field-initializers run
            // before the constructor body. If something inside ImageProcessor
            // threw an uncaught exception, the window would never get to
            // render (neither the combo box nor the labels), same as before.
            //
            // ImageProcessor's constructor no longer starts Python — that only
            // happens if the user checks the "Use AI" checkbox. So the app
            // always starts instantly, in Lanczos3 mode.
            _processor = new ImageProcessor();

            LoadResolutions();
            ShowActiveMode();
        }

        private void LoadResolutions()
        {
            comboBox_resolution.Items.Add(new ResolutionOption { Name = "Original Size (x4)", IsNativeSize = true });
            comboBox_resolution.Items.Add(new ResolutionOption { Name = "SD (480p)", Width = 720, Height = 480 });
            comboBox_resolution.Items.Add(new ResolutionOption { Name = "HD (720p)", Width = 1280, Height = 720 });
            comboBox_resolution.Items.Add(new ResolutionOption { Name = "Full HD (1080p)", Width = 1920, Height = 1080 });
            comboBox_resolution.Items.Add(new ResolutionOption { Name = "2K (DCI)", Width = 2048, Height = 1080 });
            comboBox_resolution.Items.Add(new ResolutionOption { Name = "QHD / 1440p", Width = 2560, Height = 1440 });
            comboBox_resolution.Items.Add(new ResolutionOption { Name = "4K UHD", Width = 3840, Height = 2160 });
            comboBox_resolution.Items.Add(new ResolutionOption { Name = "4K DCI", Width = 4096, Height = 2160 });
            comboBox_resolution.Items.Add(new ResolutionOption { Name = "5K", Width = 5120, Height = 2880 });
            comboBox_resolution.Items.Add(new ResolutionOption { Name = "8K UHD", Width = 7680, Height = 4320 });
            comboBox_resolution.SelectedIndex = 6; // 4K UHD (index shifted by the new entry at 0)
        }

        /// <summary>
        /// Shows which engine is active in the status label.
        /// Tells the user whether they have AI + GPU, AI + CPU, or just Lanczos.
        /// </summary>
        private void ShowActiveMode()
        {
            if (_processor.UseAI)
            {
                string mode = _processor.AIOnGPU
                    ? "🚀 Mode: AI + GPU (CUDA active)"
                    : "🧠 Mode: AI + CPU (CUDA not available)";
                UpdateStatus(mode, System.Drawing.Color.DarkGreen);
            }
            else if (_processor.LastModelError is not null)
            {
                // AI activation was attempted but failed.
                UpdateStatus(
                    $"⚡ Mode: Lanczos3 ({SummarizeError(_processor.LastModelError)})",
                    System.Drawing.Color.DarkOrange);
            }
            else
            {
                // Default normal state: nobody has requested AI yet.
                UpdateStatus("⚡ Mode: Lanczos3", System.Drawing.Color.DarkOrange);
            }
        }

        /// <summary>
        /// Fires when the "Use AI" checkbox is checked/unchecked.
        /// On the first check, it starts the Python server in the background
        /// (may take several seconds or minutes the first time it
        /// downloads the model weights) without freezing the window.
        /// On uncheck, it simply stops using it — it does not kill the process,
        /// so re-checking it in the same session is instant.
        /// </summary>
        private async void checkBox_useAI_CheckedChanged(object sender, EventArgs e)
        {
            if (!checkBox_useAI.Checked)
            {
                _processor.UseAI = false;
                ShowActiveMode();
                return;
            }

            if (_processor.AIAvailable)
            {
                // Already started earlier in this session, no need to wait for anything.
                _processor.UseAI = true;
                ShowActiveMode();
                return;
            }

            // First time it's activated: disable controls while
            // Python starts, because it can take a while (downloading weights
            // the first time, or several seconds loading the model every time).
            checkBox_useAI.Enabled = false;
            btnProcess.Enabled = false;
            UpdateStatus("⏳ Starting AI (this may take a moment)...",
                System.Drawing.Color.DarkBlue);

            bool ready = await Task.Run(() => _processor.StartAI());

            checkBox_useAI.Enabled = true;
            btnProcess.Enabled = true;

            if (!ready)
            {
                // Could not start; uncheck the checkbox and show the reason.
                checkBox_useAI.Checked = false;
                return; // The Checked change itself will trigger this method again.
            }

            _processor.UseAI = true;
            ShowActiveMode();
        }

        /// <summary>
        /// Trims long exception messages (typical of Python errors
        /// or subprocess startup errors) so they fit in the status label
        /// without overflowing the UI.
        /// </summary>
        private static string SummarizeError(string message)
        {
            const int maxLength = 80;
            string firstLine = message.Split('\n')[0].Trim();
            return firstLine.Length > maxLength
                ? firstLine[..maxLength] + "…"
                : firstLine;
        }

        private void btnSelect_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Select your images",
                Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp",
                Multiselect = true
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                selectedFiles = dialog.FileNames;
                txtInputPath.Text = $"{selectedFiles.Length} images selected";
                UpdateStatus("Images ready. Configure and process.", System.Drawing.Color.Black);
            }
        }

        private async void btnProcess_Click(object sender, EventArgs e)
        {
            if (_cts != null)
            {
                _cts.Cancel();
                return;
            }

            if (selectedFiles == null || selectedFiles.Length == 0)
            {
                MessageBox.Show("Please select images first.", "Notice",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (comboBox_resolution.SelectedItem is not ResolutionOption option)
            {
                MessageBox.Show("Please select a resolution.", "Notice",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (option.IsNativeSize && !checkBox_useAI.Checked)
            {
                // Lanczos3 is a resize algorithm — it has no "native" output size
                // without a target to resize to, unlike the AI model which always
                // produces a fixed x4 result on its own.
                MessageBox.Show(
                    "\"Original Size (x4)\" requires AI mode. Please check \"Use AI\" first, " +
                    "or pick a fixed resolution to use with Lanczos3.",
                    "Notice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _cts = new CancellationTokenSource();
            btnProcess.Text = "⛔ Cancel";
            btnSelect.Enabled = false;
            progressBar.Value = 0;
            progressBar.Maximum = selectedFiles.Length * 2; 

            string suffix = option.IsNativeSize
                ? "OriginalX4"
                : option.Name.Replace(" ", "").Replace("/", "-");
            bool fillSpaces = checkBox_fillSpaces.Checked;

            var progress = new Progress<(int current, int total, string file)>(p =>
            {
                progressBar.Value = p.current;
                UpdateStatus($"Processing {p.current} of {p.total}: {p.file}",
                    System.Drawing.Color.Black);
            });

            CancellationToken currentToken = _cts.Token;

            try
            {
                var result = await Task.Run(() =>
                    _processor.ProcessBatch(
                        selectedFiles,
                        option.Width, option.Height, option.IsNativeSize,
                        suffix, fillSpaces,
                        progress, currentToken
                    ));

                if (currentToken.IsCancellationRequested)
                {
                    UpdateStatus("Process cancelled.", System.Drawing.Color.OrangeRed);
                    progressBar.Value = 0;
                }
                else
                {
                    // Report which engine was used in the final summary
                    string engine = result.UsedAI
                        ? (result.UsedGPU ? "AI + GPU" : "AI + CPU")
                        : "Lanczos3";

                    string message =
                        $"✅ {result.TotalSuccessful} images processed successfully.\n" +
                        $"⚙️ Engine used: {engine}";

                    if (result.Errors.Count > 0)
                        message += $"\n\n⚠️ {result.Errors.Count} images failed:\n" +
                                   string.Join("\n", result.Errors);

                    UpdateStatus("Done!", System.Drawing.Color.Green);
                    MessageBox.Show(message, "Result", MessageBoxButtons.OK,
                        result.Errors.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus("Error.", System.Drawing.Color.Red);
                MessageBox.Show($"General error: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
                btnProcess.Text = "Process Images";
                btnSelect.Enabled = true;
            }
        }

        private void UpdateStatus(string message, System.Drawing.Color color)
        {
            lblStatus.Text = message;
            lblStatus.ForeColor = color;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // Release the HTTP client and kill the Python server
            // subprocess (frees the GPU/CPU it was using) on app close.
            _processor.Dispose();
            base.OnFormClosed(e);
        }
    }
}

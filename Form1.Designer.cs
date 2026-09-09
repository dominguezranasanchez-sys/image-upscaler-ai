namespace ImageUpscalerAI
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            btnSelect = new Button();
            txtInputPath = new TextBox();
            btnProcess = new Button();
            lblStatus = new Label();
            checkBox_fillSpaces = new CheckBox();
            checkBox_useAI = new CheckBox();
            comboBox_resolution = new ComboBox();
            label1 = new Label();
            progressBar = new ProgressBar();
            SuspendLayout();
            // 
            // btnSelect
            // 
            btnSelect.Location = new Point(12, 12);
            btnSelect.Name = "btnSelect";
            btnSelect.Size = new Size(187, 23);
            btnSelect.TabIndex = 0;
            btnSelect.Text = "Select Image";
            btnSelect.UseVisualStyleBackColor = true;
            btnSelect.Click += btnSelect_Click;
            // 
            // txtInputPath
            // 
            txtInputPath.Location = new Point(12, 41);
            txtInputPath.Name = "txtInputPath";
            txtInputPath.Size = new Size(187, 23);
            txtInputPath.TabIndex = 1;
            // 
            // btnProcess
            // 
            btnProcess.Location = new Point(12, 176);
            btnProcess.Name = "btnProcess";
            btnProcess.Size = new Size(187, 23);
            btnProcess.TabIndex = 2;
            btnProcess.Text = "Process Image";
            btnProcess.UseVisualStyleBackColor = true;
            btnProcess.Click += btnProcess_Click;
            // 
            // lblStatus
            // 
            lblStatus.Location = new Point(12, 202);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(187, 43);
            lblStatus.TabIndex = 3;
            lblStatus.Text = "label1";
            lblStatus.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // checkBox_fillSpaces
            // 
            checkBox_fillSpaces.AutoSize = true;
            checkBox_fillSpaces.Location = new Point(12, 70);
            checkBox_fillSpaces.Name = "checkBox_fillSpaces";
            checkBox_fillSpaces.Size = new Size(112, 19);
            checkBox_fillSpaces.TabIndex = 4;
            checkBox_fillSpaces.Text = "Fill Empty Space";
            checkBox_fillSpaces.UseVisualStyleBackColor = true;
            // 
            // checkBox_useAI
            // 
            checkBox_useAI.AutoSize = true;
            checkBox_useAI.Location = new Point(12, 95);
            checkBox_useAI.Name = "checkBox_useAI";
            checkBox_useAI.Size = new Size(180, 19);
            checkBox_useAI.TabIndex = 8;
            checkBox_useAI.Text = "Use AI (slower, better quality)";
            checkBox_useAI.UseVisualStyleBackColor = true;
            checkBox_useAI.CheckedChanged += checkBox_useAI_CheckedChanged;
            // 
            // comboBox_resolution
            // 
            comboBox_resolution.FormattingEnabled = true;
            comboBox_resolution.Location = new Point(12, 147);
            comboBox_resolution.Name = "comboBox_resolution";
            comboBox_resolution.Size = new Size(187, 23);
            comboBox_resolution.TabIndex = 5;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(12, 129);
            label1.Name = "label1";
            label1.Size = new Size(103, 15);
            label1.TabIndex = 6;
            label1.Text = "Select Resolution: ";
            // 
            // progressBar
            // 
            progressBar.Location = new Point(57, 248);
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(100, 23);
            progressBar.TabIndex = 7;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(211, 277);
            Controls.Add(progressBar);
            Controls.Add(label1);
            Controls.Add(comboBox_resolution);
            Controls.Add(checkBox_useAI);
            Controls.Add(checkBox_fillSpaces);
            Controls.Add(lblStatus);
            Controls.Add(btnProcess);
            Controls.Add(txtInputPath);
            Controls.Add(btnSelect);
            Name = "Form1";
            Text = "Form1";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btnSelect;
        private TextBox txtInputPath;
        private Button btnProcess;
        private Label lblStatus;
        private CheckBox checkBox_fillSpaces;
        private CheckBox checkBox_useAI;
        private ComboBox comboBox_resolution;
        private Label label1;
        private ProgressBar progressBar;
    }
}

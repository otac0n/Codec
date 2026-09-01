namespace Codec.UI.WinForms
{
    partial class ProgressForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.progressBar = new ProgressBar();
            this.cancelButton = new Button();
            this.progressText = new Label();
            this.SuspendLayout();
            // 
            // progressBar
            // 
            this.progressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.progressBar.Location = new Point(12, 34);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new Size(447, 34);
            this.progressBar.TabIndex = 0;
            // 
            // cancelButton
            // 
            this.cancelButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.cancelButton.Location = new Point(347, 101);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new Size(112, 34);
            this.cancelButton.TabIndex = 1;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            this.cancelButton.Click += this.CancelButton_Click;
            // 
            // progressText
            // 
            this.progressText.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            this.progressText.AutoSize = true;
            this.progressText.Location = new Point(12, 106);
            this.progressText.Name = "progressText";
            this.progressText.Size = new Size(0, 25);
            this.progressText.TabIndex = 2;
            // 
            // ProgressForm
            // 
            this.AutoScaleDimensions = new SizeF(10F, 25F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(471, 147);
            this.Controls.Add(this.progressText);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.progressBar);
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            this.Name = "ProgressForm";
            this.Text = "Progress";
            this.FormClosing += this.ProgressForm_FormClosing;
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private ProgressBar progressBar;
        private Button cancelButton;
        private Label progressText;
    }
}

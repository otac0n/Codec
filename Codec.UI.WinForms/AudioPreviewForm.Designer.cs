namespace Codec.UI.WinForms
{
    partial class AudioPreviewForm
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
            this.progress = new ProgressBar();
            this.buttonStrip = new ToolStrip();
            this.stop = new ToolStripButton();
            this.playPause = new ToolStripButton();
            this.layout = new TableLayoutPanel();
            this.buttonStrip.SuspendLayout();
            this.layout.SuspendLayout();
            this.SuspendLayout();
            // 
            // progress
            // 
            this.layout.SetColumnSpan(this.progress, 3);
            this.progress.Dock = DockStyle.Bottom;
            this.progress.Location = new Point(23, 36);
            this.progress.Maximum = 30000;
            this.progress.Name = "progress";
            this.progress.Size = new Size(332, 23);
            this.progress.TabIndex = 1;
            // 
            // buttonStrip
            // 
            this.buttonStrip.Dock = DockStyle.None;
            this.buttonStrip.GripStyle = ToolStripGripStyle.Hidden;
            this.buttonStrip.ImageScalingSize = new Size(24, 24);
            this.buttonStrip.Items.AddRange(new ToolStripItem[] { this.stop, this.playPause });
            this.buttonStrip.Location = new Point(153, 62);
            this.buttonStrip.Name = "buttonStrip";
            this.buttonStrip.RenderMode = ToolStripRenderMode.System;
            this.buttonStrip.Size = new Size(72, 33);
            this.buttonStrip.TabIndex = 0;
            this.buttonStrip.Text = "toolStrip1";
            // 
            // stop
            // 
            this.stop.DisplayStyle = ToolStripItemDisplayStyle.Image;
            this.stop.Image = Properties.Resources.FontAwesome_StopSolid_20x20;
            this.stop.ImageTransparentColor = Color.Magenta;
            this.stop.Name = "stop";
            this.stop.Size = new Size(34, 28);
            this.stop.Text = "Stop";
            this.stop.Click += this.Stop_Click;
            // 
            // playPause
            // 
            this.playPause.DisplayStyle = ToolStripItemDisplayStyle.Image;
            this.playPause.Image = Properties.Resources.FontAwesome_PlaySolid_20x20;
            this.playPause.ImageTransparentColor = Color.Magenta;
            this.playPause.Name = "playPause";
            this.playPause.Size = new Size(34, 28);
            this.playPause.Text = "toolStripButton3";
            this.playPause.Click += this.PlayPause_Click;
            // 
            // layout
            // 
            this.layout.ColumnCount = 5;
            this.layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
            this.layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            this.layout.ColumnStyles.Add(new ColumnStyle());
            this.layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            this.layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
            this.layout.Controls.Add(this.progress, 1, 0);
            this.layout.Controls.Add(this.buttonStrip, 2, 1);
            this.layout.Dock = DockStyle.Fill;
            this.layout.Location = new Point(0, 0);
            this.layout.Name = "layout";
            this.layout.RowCount = 2;
            this.layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            this.layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            this.layout.Size = new Size(379, 124);
            this.layout.TabIndex = 0;
            // 
            // AudioPreviewForm
            // 
            this.AutoScaleDimensions = new SizeF(10F, 25F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(379, 124);
            this.Controls.Add(this.layout);
            this.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            this.Name = "AudioPreviewForm";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Audio Preview";
            this.FormClosed += this.Form_FormClosed;
            this.Shown += this.AudioPreviewForm_Shown;
            this.buttonStrip.ResumeLayout(false);
            this.buttonStrip.PerformLayout();
            this.layout.ResumeLayout(false);
            this.layout.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private ProgressBar progress;
        private ToolStrip buttonStrip;
        private ToolStripButton stop;
        private ToolStripButton playPause;
        private TableLayoutPanel layout;
    }
}

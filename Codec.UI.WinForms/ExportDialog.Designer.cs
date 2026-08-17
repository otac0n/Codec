namespace Codec.UI.WinForms
{
    partial class ExportDialog
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
            this.tableLayoutPanel1 = new TableLayoutPanel();
            this.destinationFolderLabel = new Label();
            this.includePatternLabel = new Label();
            this.includeReferencedFilesCheckBox = new CheckBox();
            this.exportSubfoldersCheckBox = new CheckBox();
            this.includeArchivesCheckBox = new CheckBox();
            this.depthLabel = new Label();
            this.convertModelsCheckBox = new CheckBox();
            this.convertImagesCheckBox = new CheckBox();
            this.convertAudioCheckBox = new CheckBox();
            this.modelsFormatLabel = new Label();
            this.imagesFormatLabel = new Label();
            this.audioFormatLabel = new Label();
            this.footerPanel = new Panel();
            this.okButton = new Button();
            this.cancelButton = new Button();
            this.includePattern = new TextBox();
            this.modelsFormat = new TextBox();
            this.imagesFormat = new TextBox();
            this.audioFormat = new TextBox();
            this.depth = new NumericUpDown();
            this.folderBrowserPanel = new TableLayoutPanel();
            this.destinationFolder = new TextBox();
            this.browseForFolderButton = new Button();
            this.folderBrowserDialog1 = new FolderBrowserDialog();
            this.tableLayoutPanel1.SuspendLayout();
            this.footerPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)this.depth).BeginInit();
            this.folderBrowserPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.tableLayoutPanel1.ColumnCount = 3;
            this.tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle());
            this.tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle());
            this.tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.destinationFolderLabel, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.includePatternLabel, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.includeReferencedFilesCheckBox, 1, 2);
            this.tableLayoutPanel1.Controls.Add(this.exportSubfoldersCheckBox, 1, 3);
            this.tableLayoutPanel1.Controls.Add(this.includeArchivesCheckBox, 1, 4);
            this.tableLayoutPanel1.Controls.Add(this.depthLabel, 1, 5);
            this.tableLayoutPanel1.Controls.Add(this.convertModelsCheckBox, 1, 6);
            this.tableLayoutPanel1.Controls.Add(this.convertImagesCheckBox, 1, 8);
            this.tableLayoutPanel1.Controls.Add(this.convertAudioCheckBox, 1, 10);
            this.tableLayoutPanel1.Controls.Add(this.modelsFormatLabel, 1, 7);
            this.tableLayoutPanel1.Controls.Add(this.imagesFormatLabel, 1, 9);
            this.tableLayoutPanel1.Controls.Add(this.audioFormatLabel, 1, 11);
            this.tableLayoutPanel1.Controls.Add(this.footerPanel, 1, 13);
            this.tableLayoutPanel1.Controls.Add(this.includePattern, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.modelsFormat, 2, 7);
            this.tableLayoutPanel1.Controls.Add(this.imagesFormat, 2, 9);
            this.tableLayoutPanel1.Controls.Add(this.audioFormat, 2, 11);
            this.tableLayoutPanel1.Controls.Add(this.depth, 2, 5);
            this.tableLayoutPanel1.Controls.Add(this.folderBrowserPanel, 1, 0);
            this.tableLayoutPanel1.Location = new Point(12, 12);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 14;
            this.tableLayoutPanel1.RowStyles.Add(new RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new RowStyle());
            this.tableLayoutPanel1.Size = new Size(464, 506);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // destinationFolderLabel
            // 
            this.destinationFolderLabel.AutoSize = true;
            this.destinationFolderLabel.Location = new Point(3, 0);
            this.destinationFolderLabel.Name = "destinationFolderLabel";
            this.destinationFolderLabel.Size = new Size(161, 25);
            this.destinationFolderLabel.TabIndex = 0;
            this.destinationFolderLabel.Text = "Destination Folder:";
            // 
            // includePatternLabel
            // 
            this.includePatternLabel.AutoSize = true;
            this.includePatternLabel.Location = new Point(3, 39);
            this.includePatternLabel.Name = "includePatternLabel";
            this.includePatternLabel.Size = new Size(133, 25);
            this.includePatternLabel.TabIndex = 2;
            this.includePatternLabel.Text = "Include Pattern:";
            // 
            // includeReferencedFilesCheckBox
            // 
            this.includeReferencedFilesCheckBox.AutoSize = true;
            this.includeReferencedFilesCheckBox.Checked = true;
            this.includeReferencedFilesCheckBox.CheckState = CheckState.Checked;
            this.tableLayoutPanel1.SetColumnSpan(this.includeReferencedFilesCheckBox, 2);
            this.includeReferencedFilesCheckBox.Location = new Point(170, 79);
            this.includeReferencedFilesCheckBox.Name = "includeReferencedFilesCheckBox";
            this.includeReferencedFilesCheckBox.Size = new Size(226, 29);
            this.includeReferencedFilesCheckBox.TabIndex = 4;
            this.includeReferencedFilesCheckBox.Text = "Include Referenced Files";
            this.includeReferencedFilesCheckBox.UseVisualStyleBackColor = true;
            this.includeReferencedFilesCheckBox.CheckedChanged += this.UpdateVisibility;
            // 
            // exportSubfoldersCheckBox
            // 
            this.exportSubfoldersCheckBox.AutoSize = true;
            this.exportSubfoldersCheckBox.Checked = true;
            this.exportSubfoldersCheckBox.CheckState = CheckState.Checked;
            this.tableLayoutPanel1.SetColumnSpan(this.exportSubfoldersCheckBox, 2);
            this.exportSubfoldersCheckBox.Location = new Point(170, 114);
            this.exportSubfoldersCheckBox.Name = "exportSubfoldersCheckBox";
            this.exportSubfoldersCheckBox.Size = new Size(180, 29);
            this.exportSubfoldersCheckBox.TabIndex = 5;
            this.exportSubfoldersCheckBox.Text = "Export Subfolders";
            this.exportSubfoldersCheckBox.UseVisualStyleBackColor = true;
            this.exportSubfoldersCheckBox.CheckedChanged += this.UpdateVisibility;
            // 
            // includeArchivesCheckBox
            // 
            this.includeArchivesCheckBox.AutoSize = true;
            this.includeArchivesCheckBox.Checked = true;
            this.includeArchivesCheckBox.CheckState = CheckState.Checked;
            this.tableLayoutPanel1.SetColumnSpan(this.includeArchivesCheckBox, 2);
            this.includeArchivesCheckBox.Location = new Point(170, 149);
            this.includeArchivesCheckBox.Name = "includeArchivesCheckBox";
            this.includeArchivesCheckBox.Size = new Size(166, 29);
            this.includeArchivesCheckBox.TabIndex = 6;
            this.includeArchivesCheckBox.Text = "Include Archives";
            this.includeArchivesCheckBox.UseVisualStyleBackColor = true;
            this.includeArchivesCheckBox.CheckedChanged += this.UpdateVisibility;
            // 
            // depthLabel
            // 
            this.depthLabel.AutoSize = true;
            this.depthLabel.Dock = DockStyle.Fill;
            this.depthLabel.Location = new Point(170, 181);
            this.depthLabel.Name = "depthLabel";
            this.depthLabel.Size = new Size(73, 37);
            this.depthLabel.TabIndex = 7;
            this.depthLabel.Text = "Depth:";
            this.depthLabel.TextAlign = ContentAlignment.MiddleRight;
            // 
            // convertModelsCheckBox
            // 
            this.convertModelsCheckBox.AutoSize = true;
            this.tableLayoutPanel1.SetColumnSpan(this.convertModelsCheckBox, 2);
            this.convertModelsCheckBox.Location = new Point(170, 221);
            this.convertModelsCheckBox.Name = "convertModelsCheckBox";
            this.convertModelsCheckBox.Size = new Size(164, 29);
            this.convertModelsCheckBox.TabIndex = 9;
            this.convertModelsCheckBox.Text = "Convert Models";
            this.convertModelsCheckBox.UseVisualStyleBackColor = true;
            this.convertModelsCheckBox.CheckedChanged += this.UpdateVisibility;
            // 
            // convertImagesCheckBox
            // 
            this.convertImagesCheckBox.AutoSize = true;
            this.tableLayoutPanel1.SetColumnSpan(this.convertImagesCheckBox, 2);
            this.convertImagesCheckBox.Location = new Point(170, 293);
            this.convertImagesCheckBox.Name = "convertImagesCheckBox";
            this.convertImagesCheckBox.Size = new Size(163, 29);
            this.convertImagesCheckBox.TabIndex = 12;
            this.convertImagesCheckBox.Text = "Convert Images";
            this.convertImagesCheckBox.UseVisualStyleBackColor = true;
            this.convertImagesCheckBox.CheckedChanged += this.UpdateVisibility;
            // 
            // convertAudioCheckBox
            // 
            this.convertAudioCheckBox.AutoSize = true;
            this.tableLayoutPanel1.SetColumnSpan(this.convertAudioCheckBox, 2);
            this.convertAudioCheckBox.Enabled = false;
            this.convertAudioCheckBox.Location = new Point(170, 365);
            this.convertAudioCheckBox.Name = "convertAudioCheckBox";
            this.convertAudioCheckBox.Size = new Size(153, 29);
            this.convertAudioCheckBox.TabIndex = 15;
            this.convertAudioCheckBox.Text = "Convert Audio";
            this.convertAudioCheckBox.UseVisualStyleBackColor = true;
            this.convertAudioCheckBox.CheckedChanged += this.UpdateVisibility;
            // 
            // modelsFormatLabel
            // 
            this.modelsFormatLabel.AutoSize = true;
            this.modelsFormatLabel.Dock = DockStyle.Fill;
            this.modelsFormatLabel.Location = new Point(170, 253);
            this.modelsFormatLabel.Name = "modelsFormatLabel";
            this.modelsFormatLabel.Size = new Size(73, 37);
            this.modelsFormatLabel.TabIndex = 10;
            this.modelsFormatLabel.Text = "Format:";
            this.modelsFormatLabel.TextAlign = ContentAlignment.MiddleRight;
            // 
            // imagesFormatLabel
            // 
            this.imagesFormatLabel.AutoSize = true;
            this.imagesFormatLabel.Dock = DockStyle.Fill;
            this.imagesFormatLabel.Location = new Point(170, 325);
            this.imagesFormatLabel.Name = "imagesFormatLabel";
            this.imagesFormatLabel.Size = new Size(73, 37);
            this.imagesFormatLabel.TabIndex = 13;
            this.imagesFormatLabel.Text = "Format:";
            this.imagesFormatLabel.TextAlign = ContentAlignment.MiddleRight;
            // 
            // audioFormatLabel
            // 
            this.audioFormatLabel.AutoSize = true;
            this.audioFormatLabel.Dock = DockStyle.Fill;
            this.audioFormatLabel.Location = new Point(170, 397);
            this.audioFormatLabel.Name = "audioFormatLabel";
            this.audioFormatLabel.Size = new Size(73, 37);
            this.audioFormatLabel.TabIndex = 16;
            this.audioFormatLabel.Text = "Format:";
            this.audioFormatLabel.TextAlign = ContentAlignment.MiddleRight;
            // 
            // footerPanel
            // 
            this.tableLayoutPanel1.SetColumnSpan(this.footerPanel, 2);
            this.footerPanel.Controls.Add(this.okButton);
            this.footerPanel.Controls.Add(this.cancelButton);
            this.footerPanel.Location = new Point(170, 463);
            this.footerPanel.Name = "footerPanel";
            this.footerPanel.Size = new Size(260, 40);
            this.footerPanel.TabIndex = 18;
            // 
            // okButton
            // 
            this.okButton.Location = new Point(3, 3);
            this.okButton.Name = "okButton";
            this.okButton.Size = new Size(112, 34);
            this.okButton.TabIndex = 0;
            this.okButton.Text = "OK";
            this.okButton.UseVisualStyleBackColor = true;
            this.okButton.Click += this.OkButton_Click;
            // 
            // cancelButton
            // 
            this.cancelButton.Location = new Point(121, 3);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new Size(112, 34);
            this.cancelButton.TabIndex = 1;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            this.cancelButton.Click += this.CancelButton_Click;
            // 
            // includePattern
            // 
            this.tableLayoutPanel1.SetColumnSpan(this.includePattern, 2);
            this.includePattern.Dock = DockStyle.Fill;
            this.includePattern.Location = new Point(170, 42);
            this.includePattern.Name = "includePattern";
            this.includePattern.Size = new Size(291, 31);
            this.includePattern.TabIndex = 3;
            this.includePattern.Text = "*.*";
            // 
            // modelsFormat
            // 
            this.modelsFormat.Dock = DockStyle.Fill;
            this.modelsFormat.Location = new Point(249, 256);
            this.modelsFormat.Name = "modelsFormat";
            this.modelsFormat.Size = new Size(212, 31);
            this.modelsFormat.TabIndex = 11;
            this.modelsFormat.Text = "glb";
            // 
            // imagesFormat
            // 
            this.imagesFormat.Dock = DockStyle.Fill;
            this.imagesFormat.Location = new Point(249, 328);
            this.imagesFormat.Name = "imagesFormat";
            this.imagesFormat.Size = new Size(212, 31);
            this.imagesFormat.TabIndex = 14;
            this.imagesFormat.Text = "png";
            // 
            // audioFormat
            // 
            this.audioFormat.Dock = DockStyle.Fill;
            this.audioFormat.Location = new Point(249, 400);
            this.audioFormat.Name = "audioFormat";
            this.audioFormat.Size = new Size(212, 31);
            this.audioFormat.TabIndex = 17;
            this.audioFormat.Text = "wav";
            // 
            // depth
            // 
            this.depth.Dock = DockStyle.Fill;
            this.depth.Location = new Point(249, 184);
            this.depth.Maximum = new decimal(new int[] { 200, 0, 0, 0 });
            this.depth.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.depth.Name = "depth";
            this.depth.Size = new Size(212, 31);
            this.depth.TabIndex = 8;
            this.depth.Value = new decimal(new int[] { 10, 0, 0, 0 });
            // 
            // folderBrowserPanel
            // 
            this.folderBrowserPanel.ColumnCount = 2;
            this.tableLayoutPanel1.SetColumnSpan(this.folderBrowserPanel, 2);
            this.folderBrowserPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.folderBrowserPanel.ColumnStyles.Add(new ColumnStyle());
            this.folderBrowserPanel.Controls.Add(this.destinationFolder, 0, 0);
            this.folderBrowserPanel.Controls.Add(this.browseForFolderButton, 1, 0);
            this.folderBrowserPanel.Dock = DockStyle.Fill;
            this.folderBrowserPanel.Location = new Point(170, 3);
            this.folderBrowserPanel.Name = "folderBrowserPanel";
            this.folderBrowserPanel.RowCount = 1;
            this.folderBrowserPanel.RowStyles.Add(new RowStyle());
            this.folderBrowserPanel.Size = new Size(291, 33);
            this.folderBrowserPanel.TabIndex = 1;
            // 
            // destinationFolder
            // 
            this.destinationFolder.Dock = DockStyle.Fill;
            this.destinationFolder.Location = new Point(3, 3);
            this.destinationFolder.Name = "destinationFolder";
            this.destinationFolder.Size = new Size(253, 31);
            this.destinationFolder.TabIndex = 0;
            this.destinationFolder.TextChanged += this.UpdateVisibility;
            // 
            // browseForFolderButton
            // 
            this.browseForFolderButton.AutoSize = true;
            this.browseForFolderButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.browseForFolderButton.Image = Properties.Resources.FontAwesome_FolderOpenSolid_20x20;
            this.browseForFolderButton.Location = new Point(259, 0);
            this.browseForFolderButton.Margin = new Padding(0);
            this.browseForFolderButton.Name = "browseForFolderButton";
            this.browseForFolderButton.Padding = new Padding(3);
            this.browseForFolderButton.Size = new Size(32, 32);
            this.browseForFolderButton.TabIndex = 1;
            this.browseForFolderButton.UseVisualStyleBackColor = true;
            this.browseForFolderButton.Click += this.BrowseForFolderButton_Click;
            // 
            // ExportDialog
            // 
            this.AcceptButton = this.okButton;
            this.AutoScaleDimensions = new SizeF(10F, 25F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.CancelButton = this.cancelButton;
            this.ClientSize = new Size(488, 530);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "ExportDialog";
            this.Text = "Export Files";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.footerPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)this.depth).EndInit();
            this.folderBrowserPanel.ResumeLayout(false);
            this.folderBrowserPanel.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel tableLayoutPanel1;
        private Label destinationFolderLabel;
        private Label includePatternLabel;
        private CheckBox includeReferencedFilesCheckBox;
        private CheckBox exportSubfoldersCheckBox;
        private CheckBox includeArchivesCheckBox;
        private Label depthLabel;
        private CheckBox convertModelsCheckBox;
        private CheckBox convertImagesCheckBox;
        private CheckBox convertAudioCheckBox;
        private Label modelsFormatLabel;
        private Label imagesFormatLabel;
        private Label audioFormatLabel;
        private Panel footerPanel;
        private TextBox includePattern;
        private TextBox modelsFormat;
        private TextBox imagesFormat;
        private TextBox audioFormat;
        private NumericUpDown depth;
        private Button okButton;
        private Button cancelButton;
        private TableLayoutPanel folderBrowserPanel;
        private TextBox destinationFolder;
        private Button browseForFolderButton;
        private FolderBrowserDialog folderBrowserDialog1;
    }
}
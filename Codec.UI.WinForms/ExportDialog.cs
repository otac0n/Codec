namespace Codec.UI.WinForms
{
    using System;
    using System.Windows.Forms;
    using Codec.Services;
    using EntryItem = (Codec.Archives.Entry Entry, Codec.Services.EntryType EntryType);

    public partial class ExportDialog : Form
    {
        private readonly bool anyFolders;
        private readonly bool anyAudio;
        private readonly bool anyImages;
        private readonly bool anyModels;

        private bool IncludeReferences => this.includeReferencedFilesCheckBox.Checked;

        private bool Recursive => this.exportSubfoldersCheckBox.Checked;

        private bool RecurseArchives => this.includeArchivesCheckBox.Checked;

        private bool ConvertAudio => this.convertAudioCheckBox.Checked;

        private bool ConvertImages => this.convertImagesCheckBox.Checked;

        private bool ConvertModels => this.convertModelsCheckBox.Checked;

        public ExportDialog(IList<EntryItem> entryItems)
        {
            this.InitializeComponent();
            this.anyFolders = entryItems.Any(e => e.Entry.CanEnumerateEntries);
            this.anyAudio = this.anyFolders || entryItems.Any(e => e.EntryType == EntryType.Audio);
            this.anyImages = this.anyFolders || entryItems.Any(e => e.EntryType == EntryType.Image);
            this.anyModels = this.anyFolders || entryItems.Any(e => e.EntryType == EntryType.Model);
            this.UpdateVisibility(this, EventArgs.Empty);
        }

        public FileExportService.ExportConfig GetConfiguration()
        {
            return new()
            {
                Destination = this.destinationFolder.Text,
                Include = this.includePattern.Text,
                IncludeReferences = this.IncludeReferences,
                AudioFormat = this.ConvertAudio ? this.audioFormat.Text : null,
                ImageFormat = this.ConvertImages ? this.imagesFormat.Text : null,
                ModelFormat = this.ConvertModels ? this.modelsFormat.Text : null,
                Recursive = this.Recursive,
                ArchiveDepth = this.RecurseArchives ? (byte)this.depth.Value : default(byte?),
            };
        }

        private void UpdateVisibility(object sender, EventArgs e)
        {
            this.includePatternLabel.Visible = this.includePattern.Visible = this.anyFolders;
            this.includeReferencedFilesCheckBox.Visible = this.anyModels;
            this.exportSubfoldersCheckBox.Visible = this.anyFolders;
            this.includeArchivesCheckBox.Visible = this.anyFolders && this.Recursive;
            this.depthLabel.Visible = this.depth.Visible = this.anyFolders && this.Recursive && this.RecurseArchives;
            this.convertModelsCheckBox.Visible = this.anyModels;
            this.modelsFormatLabel.Visible = this.modelsFormat.Visible = this.anyModels && this.ConvertModels;
            this.convertImagesCheckBox.Visible = this.anyImages || (this.anyModels && this.IncludeReferences);
            this.imagesFormatLabel.Visible = this.imagesFormat.Visible = (this.anyImages || (this.anyModels && this.IncludeReferences)) && this.ConvertImages;
            this.convertAudioCheckBox.Visible = this.anyAudio;
            this.audioFormatLabel.Visible = this.audioFormat.Visible = this.anyAudio && this.ConvertAudio;
            this.okButton.Enabled = !string.IsNullOrWhiteSpace(this.destinationFolder.Text) && Path.IsPathFullyQualified(this.destinationFolder.Text);
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void BrowseForFolderButton_Click(object sender, EventArgs e)
        {
            this.folderBrowserDialog1.SelectedPath = this.destinationFolder.Text;
            if (this.folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                this.destinationFolder.Text = this.folderBrowserDialog1.SelectedPath;
            }
        }
    }
}

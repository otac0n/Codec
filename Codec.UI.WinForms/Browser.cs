// Copyright © John Gietzen. All Rights Reserved. This source is subject to the GPL license. Please see license.md for more information.

namespace Codec.UI.WinForms
{
    using System;
    using System.ComponentModel;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using Codec.Archives;
    using Codec.Files;
    using Codec.MGS;
    using Codec.Services;
    using ImageMagick;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    internal partial class Browser : Form
    {
        private readonly EntryTypeDetector detector;
        private readonly NestedFileSystemManager fsm;
        private readonly FileExportService exportService;
        private readonly NotifyingLoggerProvider provider;
        private readonly VirtualImageList<Entry> textureDisplay;
        private readonly List<Entry> history = [];
        private readonly List<Entry> contextEntries = [];
        private int historyIndex = -1;
        private bool suppressUpdates;

        public IList<Entry> SelectedEntries =>
            this.contextEntries.Count > 0
                ? this.contextEntries
                : [.. this.entryList.SelectedItems.Cast<ListViewItem>().Select(i => (Entry)i.Tag)];

        public Browser(IServiceProvider serviceProvider)
        {
            this.detector = serviceProvider.GetRequiredService<EntryTypeDetector>();
            this.fsm = serviceProvider.GetRequiredService<NestedFileSystemManager>();
            this.exportService = serviceProvider.GetRequiredService<FileExportService>();
            this.provider = serviceProvider.GetRequiredService<NotifyingLoggerProvider>();

            this.InitializeComponent();
            this.provider.EntryLogged += this.Provider_EntryLogged;
            this.Icon = Properties.Resources.Otacon;
            this.fileTypes.Images.AddRange([
                Properties.Resources.FontAwesome_FolderOpenSolid_20x20,
                Properties.Resources.FontAwesome_FileSolid_20x20,
                Properties.Resources.FontAwesome_BoxArchiveSolid_20x20,
                Properties.Resources.FontAwesome_FileImageSolid_20x20,
                Properties.Resources.FontAwesome_FileVideoSolid_20x20,
                Properties.Resources.FontAwesome_FileAudioSolid_20x20,
                Properties.Resources.FontAwesome_ShapesSolid_20x20,
            ]);
            this.saveSelectedDialog.InitialDirectory = Environment.ExpandEnvironmentVariables(this.saveSelectedDialog.InitialDirectory);
            this.saveToFolderDialog.InitialDirectory = Environment.ExpandEnvironmentVariables(this.saveToFolderDialog.InitialDirectory);
            this.textureDisplay = new VirtualImageList<Entry>(
                entry => Task.FromResult(this.fsm.Resolve<MagickImage>(entry.Path)?.ToBitmap()!),
                InterpolationMode.NearestNeighbor)
            {
                Dock = DockStyle.Fill,
                Visible = false,
            };
            this.textureDisplay.MouseDown += this.TextureDisplay_Click;
            this.splitContainer.Panel2.Controls.Add(this.textureDisplay);

            this.fileTree.Nodes.Add(new TreeNode("root", 0, 0, [this.CreateExpanderDummy()]) { Tag = this.fsm.RootEntry });
            this.Navigate(Path.Combine(serviceProvider.GetRequiredService<EnvironmentOptions>().SteamApps, WellKnownPaths.AllDataBin, WellKnownPaths.CD1Path, WellKnownPaths.StageDirPath));
        }

        private void Provider_EntryLogged(object? sender, NotifyingLoggerProvider.LogEntry e)
        {
            this.InvokeIfRequired(() => this.errorListView.Items.Add(new ListViewItem([e.Severity.ToString(), e.Text, e.Location])));
        }

        private void TextureDisplay_Click(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && e.Clicks == 1)
            {
                if (this.textureDisplay.HitTest(e.Location, out var hit))
                {
                    this.contextEntries.Clear();
                    this.contextEntries.Add(hit);
                    this.UpdateContextMenu();
                    this.entryContextMenu.Show(this.textureDisplay, e.Location);
                }
            }
        }

        private TreeNode CreateExpanderDummy() => new("...");

        private void Navigate(string path)
        {
            if (this.fsm.TryGetEntry(path, out var entry))
            {
                this.Navigate(entry);
            }
        }

        private void Navigate(Entry entry, bool addHistory = true)
        {
            if (addHistory)
            {
                var removeCount = (this.history.Count - 1) - this.historyIndex;
                if (removeCount > 0)
                {
                    this.history.RemoveRange(this.historyIndex, removeCount);
                }

                this.history.Add(entry);
                this.historyIndex = this.history.Count - 1;
            }

            this.suppressUpdates = true;
            this.goUpButton.Enabled = entry.Path?.IndexOfAny(PathExtensions.Separators) > -1;
            this.backButton.Enabled = this.historyIndex > 0;
            this.forwardButton.Enabled = this.historyIndex < this.history.Count - 1;
            this.pathBox.Tag = entry.Path;
            this.pathBox.Text = entry.Path;

            var currentNode = this.fileTree.Nodes[0];
            foreach (var segment in PathExtensions.SplitPath(entry.Path))
            {
                this.FileTree_BeforeExpand(this, new(currentNode, false, TreeViewAction.Unknown));
                currentNode.Expand();

                static string GetName(string path)
                {
                    var name = PathExtensions.GetFileName(path);
                    return string.IsNullOrEmpty(name) ? path : name;
                }

                var nextNode = currentNode.Nodes.Cast<TreeNode>().Where(n => n.Tag is Entry e && GetName(e.Path) == segment).FirstOrDefault();
                if (nextNode == null)
                {
                    break;
                }

                currentNode = nextNode;
            }

            this.fileTree.SelectedNode = currentNode;
            currentNode.EnsureVisible();

            var entries = this.fsm.EnumerateEntries(entry.Path);
            var items = entries
                .Select(e => new ListViewItem(this.fsm.GetFileName(e.Path) switch { "" => e.Path, var x => x }, (int)this.detector.Detect(e)) { Tag = e })
                .ToArray();
            this.entryList.Items.Clear();
            this.EntryList_SelectedIndexChanged(this.entryList, EventArgs.Empty);
            this.entryList.Items.AddRange(items);

            this.textureDisplay.Items = entries.Where(e => this.detector.Detect(e) == EntryType.Image);

            this.suppressUpdates = false;
        }

        private void FileTree_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            if (e.Node?.Tag is Entry entry && e.Node.Nodes is [TreeNode onlyChild] && onlyChild.Text == "...")
            {
                e.Node.Nodes.Clear();
                var entries = this.fsm.EnumerateEntries(entry.Path).Where(e => e.CanEnumerateEntries);
                e.Node.Nodes.AddRange([.. entries.Select(e => new TreeNode(this.fsm.GetFileName(e.Path) switch { "" => e.Path, var x => x }, 0, 0, [this.CreateExpanderDummy()]) { Tag = e })]);
            }
        }

        private void FileTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (!this.suppressUpdates && e.Node?.Tag is Entry entry)
            {
                this.Navigate(entry);
            }
        }

        private void PathBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                e.Handled = true;
                this.Navigate(this.pathBox.Text);
            }
        }

        private void PathBox_Validating(object sender, CancelEventArgs e)
        {
            if (!this.suppressUpdates && !this.pathBox.Text.Equals(this.pathBox.Tag))
            {
                this.Navigate(this.pathBox.Text);
            }
        }

        private void BackButton_Click(object sender, EventArgs e)
        {
            this.historyIndex--;
            this.Navigate(this.history[this.historyIndex], addHistory: false);
        }

        private void ForwardButton_Click(object sender, EventArgs e)
        {
            this.historyIndex++;
            this.Navigate(this.history[this.historyIndex], addHistory: false);
        }

        private void GoUpButton_Click(object sender, EventArgs e)
        {
            this.Navigate(PathExtensions.GetDirectoryName((string)this.pathBox.Tag));
        }

        private void PreviewMenuItem_Click(object sender, EventArgs e)
        {
            this.EntryList_ItemActivate(sender, e);
        }

        private async void EntryList_ItemActivate(object sender, EventArgs e)
        {
            if (this.SelectedEntries is not [Entry entry])
            {
                return;
            }

            if (entry.CanEnumerateEntries)
            {
                this.Navigate(entry);
            }
            else
            {
                switch (this.detector.Detect(entry))
                {
                    case EntryType.Image:
                        {
                            if (this.fsm.Resolve<MagickImage>(entry.Path) is MagickImage image)
                            {
                                var childForm = new Form
                                {
                                    Text = this.fsm.GetFileName(entry.Path),
                                    StartPosition = FormStartPosition.CenterParent,
                                    FormBorderStyle = FormBorderStyle.SizableToolWindow,
                                };
                                childForm.Controls.Add(new PictureBox
                                {
                                    Dock = DockStyle.Fill,
                                    SizeMode = PictureBoxSizeMode.Zoom,
                                    Image = image.ToBitmap(),
                                    BackColor = Color.Black,
                                });
                                this.ShowChild(childForm);
                            }
                        }
                        break;
                    case EntryType.Audio:
                        {
                            try
                            {
                                var audioStream = this.fsm.Resolve<AudioStream>(entry.Path) ?? (AudioStream)this.fsm.OpenRead(entry.Path);
                                var childForm = new AudioPreviewForm(audioStream)
                                {
                                    Text = this.fsm.GetFileName(entry.Path),
                                };
                                this.ShowChild(childForm);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(this, $"Failed to play audio: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                        break;
                    case EntryType.Model:
                        {
                            if (this.fsm.Resolve<RenderableScene>(entry.Path) is RenderableScene scene)
                            {
                                var childForm = new Form
                                {
                                    Text = this.fsm.GetFileName(entry.Path),
                                    StartPosition = FormStartPosition.CenterParent,
                                    FormBorderStyle = FormBorderStyle.SizableToolWindow,
                                };
                                childForm.Controls.Add(new ModelRendererControl(entry.Path, this.fsm, scene)
                                {
                                    Dock = DockStyle.Fill,
                                });
                                this.ShowChild(childForm);
                            }
                        }
                        break;
                }
            }
        }

        private async void ReplaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.SelectedEntries is [Entry entry])
            {
                await this.exportService.ReplaceSingleAsync(entry, (suggestedFileName, type, supportedPatterns) =>
                {
                    this.openFileDialog.Filter = supportedPatterns is string supportedTypes
                        ? $"{type} Files|{supportedTypes}|All Files|*.*"
                        : "All Files|*.*";

                    var result = this.openFileDialog.ShowDialog();
                    return Task.FromResult(result == DialogResult.OK ? this.openFileDialog.FileName : null);
                });
            }
        }

        private void ShowChild(Form childForm)
        {
            childForm.Owner = this;
            if (childForm.StartPosition == FormStartPosition.CenterParent)
            {
                childForm.StartPosition = FormStartPosition.Manual;
                var topLeft = this.entryList.PointToScreen(Point.Empty);
                childForm.Location = new Point(
                    Math.Max(topLeft.X + (this.entryList.Width - childForm.Width) / 2, 0),
                    Math.Max(topLeft.Y + (this.entryList.Height - childForm.Height) / 2, 0));
            }
            childForm.Show();
        }

        private void ListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.entryList.Visible = true;
            this.textureDisplay.Visible = false;
            this.listToolStripMenuItem.Checked = true;
            this.imagePreviewToolStripMenuItem.Checked = false;
        }

        private void ImagePreviewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.entryList.Visible = false;
            this.textureDisplay.Visible = true;
            this.listToolStripMenuItem.Checked = false;
            this.imagePreviewToolStripMenuItem.Checked = true;
        }

        private void EntryList_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.UpdateContextMenu();
        }

        private void UpdateContextMenu()
        {
            var selectedEntries = this.SelectedEntries;
            var onlyFiles = selectedEntries.All(e => e.CanOpen);
            this.previewToolStripMenuItem.Enabled = selectedEntries.Count == 1;
            this.previewToolStripMenuItem.Text = selectedEntries.Any(e => e.CanEnumerateEntries) ? "Open" : "Preview...";
            this.copyPathToolStripMenuItem.Enabled = selectedEntries.Count >= 1;
            this.saveAsToolStripMenuItem.Enabled = this.saveButton.Enabled = selectedEntries.Count >= 1 && onlyFiles;
            this.replaceToolStripMenuItem.Enabled = selectedEntries.Count == 1 && onlyFiles;
        }

        private void CopyPathToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var paths = string.Join(Environment.NewLine, this.SelectedEntries.Select(e => e.Path));
            Clipboard.SetText(paths);
        }

        private async void SaveButton_Click(object sender, EventArgs e)
        {
            var selectedEntries = this.SelectedEntries;
            if (selectedEntries.Count == 1)
            {
                var entry = selectedEntries[0];
                await this.exportService.SaveSingleAsync(entry, (suggestedFileName, type, supportedPatterns) =>
                {
                    this.saveSelectedDialog.Filter = supportedPatterns is string supportedTypes
                        ? $"{type} Files|{supportedTypes}|All Files|*.*"
                        : "All Files|*.*";

                    this.saveSelectedDialog.FileName = suggestedFileName;
                    var result = this.saveSelectedDialog.ShowDialog();
                    return Task.FromResult(result == DialogResult.OK ? this.saveSelectedDialog.FileName : null);
                });
            }
            else if (selectedEntries.Count >= 0)
            {
                await this.exportService.SaveMultipleAsync(
                    selectedEntries,
                    () =>
                    {
                        this.saveToFolderDialog.SelectedPath = string.Empty;
                        var result = this.saveToFolderDialog.ShowDialog();
                        return Task.FromResult(result == DialogResult.OK ? this.saveToFolderDialog.SelectedPath : null);
                    },
                    path =>
                    {
                        var overwriteResult = MessageBox.Show($"The destination path \"{path}\" already contians files with the same name. Do you want to overwrite?", "Confirm Overwrite", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                        return Task.FromResult(overwriteResult == DialogResult.Yes);
                    });
            }
        }

        private void EntryContextMenu_Closed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            // Defer untill after the click message has pumped.
            this.BeginInvoke(() =>
            {
                if (this.contextEntries.Count > 0)
                {
                    this.contextEntries.Clear();
                    this.UpdateContextMenu();
                }
            });
        }

        private void ViewErrorsButton_CheckedChanged(object sender, EventArgs e)
        {
            this.errorListView.Visible = this.viewErrorsButton.Checked;
        }
    }
}

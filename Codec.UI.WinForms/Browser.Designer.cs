namespace Codec.UI.WinForms
{
    partial class Browser
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
            this.components = new System.ComponentModel.Container();
            this.pathBox = new TextBox();
            this.splitContainer = new SplitContainer();
            this.fileTree = new TreeView();
            this.entryList = new ListView();
            this.entryContextMenu = new ContextMenuStrip(this.components);
            this.previewToolStripMenuItem = new ToolStripMenuItem();
            this.saveAsToolStripMenuItem = new ToolStripMenuItem();
            this.fileTypes = new ImageList(this.components);
            this.topToolStrip = new ToolStrip();
            this.saveButton = new ToolStripButton();
            this.viewDrowDown = new ToolStripDropDownButton();
            this.listToolStripMenuItem = new ToolStripMenuItem();
            this.imagePreviewToolStripMenuItem = new ToolStripMenuItem();
            this.toolStripSeparator1 = new ToolStripSeparator();
            this.goUpButton = new ToolStripButton();
            this.backButton = new ToolStripButton();
            this.forwardButton = new ToolStripButton();
            this.lowerStatusStrip = new StatusStrip();
            this.saveSelectedDialog = new SaveFileDialog();
            this.saveToFolderDialog = new FolderBrowserDialog();
            ((System.ComponentModel.ISupportInitialize)this.splitContainer).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.entryContextMenu.SuspendLayout();
            this.topToolStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // pathBox
            // 
            this.pathBox.Dock = DockStyle.Top;
            this.pathBox.Location = new Point(0, 33);
            this.pathBox.Name = "pathBox";
            this.pathBox.Size = new Size(1203, 31);
            this.pathBox.TabIndex = 0;
            this.pathBox.KeyPress += this.PathBox_KeyPress;
            this.pathBox.Validating += this.PathBox_Validating;
            // 
            // splitContainer
            // 
            this.splitContainer.Dock = DockStyle.Fill;
            this.splitContainer.Location = new Point(0, 64);
            this.splitContainer.Name = "splitContainer";
            // 
            // splitContainer.Panel1
            // 
            this.splitContainer.Panel1.Controls.Add(this.fileTree);
            // 
            // splitContainer.Panel2
            // 
            this.splitContainer.Panel2.Controls.Add(this.entryList);
            this.splitContainer.Size = new Size(1203, 702);
            this.splitContainer.SplitterDistance = 401;
            this.splitContainer.TabIndex = 1;
            // 
            // fileTree
            // 
            this.fileTree.Dock = DockStyle.Fill;
            this.fileTree.Location = new Point(0, 0);
            this.fileTree.Name = "fileTree";
            this.fileTree.Size = new Size(401, 702);
            this.fileTree.TabIndex = 0;
            this.fileTree.BeforeExpand += this.FileTree_BeforeExpand;
            this.fileTree.AfterSelect += this.FileTree_AfterSelect;
            // 
            // entryList
            // 
            this.entryList.ContextMenuStrip = this.entryContextMenu;
            this.entryList.Dock = DockStyle.Fill;
            this.entryList.LargeImageList = this.fileTypes;
            this.entryList.Location = new Point(0, 0);
            this.entryList.Name = "entryList";
            this.entryList.Size = new Size(798, 702);
            this.entryList.SmallImageList = this.fileTypes;
            this.entryList.TabIndex = 0;
            this.entryList.UseCompatibleStateImageBehavior = false;
            this.entryList.View = View.List;
            this.entryList.ItemActivate += this.EntryList_ItemActivate;
            this.entryList.SelectedIndexChanged += this.EntryList_SelectedIndexChanged;
            // 
            // entryContextMenu
            // 
            this.entryContextMenu.ImageScalingSize = new Size(24, 24);
            this.entryContextMenu.Items.AddRange(new ToolStripItem[] { this.previewToolStripMenuItem, this.saveAsToolStripMenuItem });
            this.entryContextMenu.Name = "entryContextMenu";
            this.entryContextMenu.Size = new Size(167, 68);
            // 
            // previewToolStripMenuItem
            // 
            this.previewToolStripMenuItem.Image = Properties.Resources.FontAwesome_EyeSolid_20x20;
            this.previewToolStripMenuItem.Name = "previewToolStripMenuItem";
            this.previewToolStripMenuItem.Size = new Size(166, 32);
            this.previewToolStripMenuItem.Text = "Preview...";
            this.previewToolStripMenuItem.Click += this.PreviewMenuItem_Click;
            // 
            // saveAsToolStripMenuItem
            // 
            this.saveAsToolStripMenuItem.Image = Properties.Resources.FontAwesome_FloppyDiskSolid_20x20;
            this.saveAsToolStripMenuItem.Name = "saveAsToolStripMenuItem";
            this.saveAsToolStripMenuItem.Size = new Size(166, 32);
            this.saveAsToolStripMenuItem.Text = "Save As...";
            this.saveAsToolStripMenuItem.Click += this.SaveButton_Click;
            // 
            // fileTypes
            // 
            this.fileTypes.ColorDepth = ColorDepth.Depth32Bit;
            this.fileTypes.ImageSize = new Size(16, 16);
            this.fileTypes.TransparentColor = Color.Transparent;
            // 
            // topToolStrip
            // 
            this.topToolStrip.ImageScalingSize = new Size(24, 24);
            this.topToolStrip.Items.AddRange(new ToolStripItem[] { this.backButton, this.forwardButton, this.goUpButton, this.toolStripSeparator1, this.saveButton, this.viewDrowDown });
            this.topToolStrip.Location = new Point(0, 0);
            this.topToolStrip.Name = "topToolStrip";
            this.topToolStrip.Size = new Size(1203, 33);
            this.topToolStrip.TabIndex = 2;
            this.topToolStrip.Text = "toolStrip1";
            // 
            // saveButton
            // 
            this.saveButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
            this.saveButton.Enabled = false;
            this.saveButton.Image = Properties.Resources.FontAwesome_FloppyDiskSolid_20x20;
            this.saveButton.ImageTransparentColor = Color.Magenta;
            this.saveButton.Name = "saveButton";
            this.saveButton.Size = new Size(34, 28);
            this.saveButton.Text = "&Save";
            this.saveButton.Click += this.SaveButton_Click;
            // 
            // viewDrowDown
            // 
            this.viewDrowDown.DisplayStyle = ToolStripItemDisplayStyle.Image;
            this.viewDrowDown.DropDownItems.AddRange(new ToolStripItem[] { this.listToolStripMenuItem, this.imagePreviewToolStripMenuItem });
            this.viewDrowDown.Image = Properties.Resources.FontAwesome_GearSolid_20x20;
            this.viewDrowDown.ImageTransparentColor = Color.Magenta;
            this.viewDrowDown.Name = "viewDrowDown";
            this.viewDrowDown.Size = new Size(42, 28);
            this.viewDrowDown.Text = "View";
            // 
            // listToolStripMenuItem
            // 
            this.listToolStripMenuItem.Checked = true;
            this.listToolStripMenuItem.CheckState = CheckState.Checked;
            this.listToolStripMenuItem.Name = "listToolStripMenuItem";
            this.listToolStripMenuItem.Size = new Size(229, 34);
            this.listToolStripMenuItem.Text = "List";
            this.listToolStripMenuItem.Click += this.ListToolStripMenuItem_Click;
            // 
            // imagePreviewToolStripMenuItem
            // 
            this.imagePreviewToolStripMenuItem.Name = "imagePreviewToolStripMenuItem";
            this.imagePreviewToolStripMenuItem.Size = new Size(229, 34);
            this.imagePreviewToolStripMenuItem.Text = "Image Preview";
            this.imagePreviewToolStripMenuItem.Click += this.ImagePreviewToolStripMenuItem_Click;
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new Size(6, 33);
            // 
            // goUpButton
            // 
            this.goUpButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
            this.goUpButton.Image = Properties.Resources.FontAwesome_ArrowTurnUpSolid_20x20;
            this.goUpButton.ImageTransparentColor = Color.Magenta;
            this.goUpButton.Name = "goUpButton";
            this.goUpButton.Size = new Size(34, 28);
            this.goUpButton.Text = "Go Up";
            this.goUpButton.Click += this.GoUpButton_Click;
            // 
            // backButton
            // 
            this.backButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
            this.backButton.Image = Properties.Resources.FontAwesome_ChevronLeftSolid_20x20;
            this.backButton.ImageTransparentColor = Color.Magenta;
            this.backButton.Name = "backButton";
            this.backButton.Size = new Size(34, 28);
            this.backButton.Text = "Back";
            this.backButton.Click += this.BackButton_Click;
            // 
            // forwardButton
            // 
            this.forwardButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
            this.forwardButton.Image = Properties.Resources.FontAwesome_ChevronRightSolid_20x20;
            this.forwardButton.ImageTransparentColor = Color.Magenta;
            this.forwardButton.Name = "forwardButton";
            this.forwardButton.Size = new Size(34, 28);
            this.forwardButton.Text = "Forward";
            this.forwardButton.Click += this.ForwardButton_Click;
            // 
            // lowerStatusStrip
            // 
            this.lowerStatusStrip.ImageScalingSize = new Size(24, 24);
            this.lowerStatusStrip.Location = new Point(0, 766);
            this.lowerStatusStrip.Name = "lowerStatusStrip";
            this.lowerStatusStrip.Size = new Size(1203, 22);
            this.lowerStatusStrip.TabIndex = 3;
            this.lowerStatusStrip.Text = "statusStrip1";
            // 
            // saveSelectedDialog
            // 
            this.saveSelectedDialog.InitialDirectory = "%USERPROFILE%\\Downloads";
            // 
            // saveToFolderDialog
            // 
            this.saveToFolderDialog.InitialDirectory = "%USERPROFILE%\\Downloads";
            this.saveToFolderDialog.RootFolder = Environment.SpecialFolder.MyComputer;
            // 
            // Browser
            // 
            this.AutoScroll = true;
            this.ClientSize = new Size(1203, 788);
            this.Controls.Add(this.splitContainer);
            this.Controls.Add(this.lowerStatusStrip);
            this.Controls.Add(this.pathBox);
            this.Controls.Add(this.topToolStrip);
            this.Name = "Browser";
            this.Text = "Codec";
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)this.splitContainer).EndInit();
            this.splitContainer.ResumeLayout(false);
            this.entryContextMenu.ResumeLayout(false);
            this.topToolStrip.ResumeLayout(false);
            this.topToolStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion
        private System.Windows.Forms.TextBox pathBox;
        private System.Windows.Forms.ListView entryList;
        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.ImageList fileTypes;
        private System.Windows.Forms.TreeView fileTree;
        private System.Windows.Forms.ToolStrip topToolStrip;
        private System.Windows.Forms.ToolStripDropDownButton viewDrowDown;
        private System.Windows.Forms.ToolStripMenuItem listToolStripMenuItem;
        private System.Windows.Forms.StatusStrip lowerStatusStrip;
        private System.Windows.Forms.ToolStripButton saveButton;
        private System.Windows.Forms.SaveFileDialog saveSelectedDialog;
        private System.Windows.Forms.FolderBrowserDialog saveToFolderDialog;
        private System.Windows.Forms.ToolStripMenuItem imagePreviewToolStripMenuItem;
        private ContextMenuStrip entryContextMenu;
        private ToolStripMenuItem saveAsToolStripMenuItem;
        private ToolStripButton goUpButton;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripButton backButton;
        private ToolStripButton forwardButton;
        private ToolStripMenuItem previewToolStripMenuItem;
    }
}

namespace Codec.UI.Avalonia.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Avalonia.Controls;
    using Codec.Archives;
    using Codec.Services;
    using Codec.UI.Avalonia.Models;
    using Codec.UI.Avalonia.Services;
    using Codec.UI.Avalonia.Views;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using Microsoft.Extensions.Logging;

    public sealed partial class EntryListViewModel : ObservableObject, IDisposable
    {
        private readonly NestedFileSystemManager fsm;
        private readonly EntryTypeDetector detector;
        private readonly FileSaveService fileSaveService;
        private readonly ImageLoader imageLoader;
        private readonly ILogger<EntryListViewModel> logger;
        private CancellationTokenSource cts = new();

        private Entry parentEntry;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Thumbnails))]
        private ObservableCollection<EntryItem> entries = [];

        private List<ThumbnailItemViewModel>? thumbnails;

        public List<ThumbnailItemViewModel> Thumbnails =>
            this.thumbnails ??= [.. this.Entries.Where(e => e.EntryType == EntryType.Image).Select(e => new ThumbnailItemViewModel(e, this.imageLoader, this.cts.Token))];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ContextEntries))]
        private ObservableCollection<EntryItem> selectedEntries = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ContextEntries))]
        private EntryItem? contextEntry = null;

        private IList<EntryItem> ContextEntries => this.ContextEntry is EntryItem entry ? [entry] : this.SelectedEntries;

        [ObservableProperty]
        private ViewMode currentViewMode = ViewMode.List;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PreviewCommand))]
        private bool canPreview;

        [ObservableProperty]
        private bool previewIsOpen;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        private bool canSave;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ReplaceCommand))]
        private bool canReplace;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CopyPathCommand))]
        private bool canCopyPath;

        public event EventHandler<IList<EntryItem>>? EntryActivated;

        public EntryListViewModel(NestedFileSystemManager fsm, EntryTypeDetector detector, FileSaveService fileSaveService, ImageLoader imageLoader, ILogger<EntryListViewModel> logger)
        {
            this.fsm = fsm;
            this.detector = detector;
            this.fileSaveService = fileSaveService;
            this.imageLoader = imageLoader;
            this.logger = logger;
            this.SelectedEntries.CollectionChanged += this.ContextChanged;
        }

        partial void OnContextEntryChanged(EntryItem? value)
        {
            this.ContextChanged(this, EventArgs.Empty);
        }

        private void ContextChanged(object? sender, EventArgs e)
        {
            var onlyFiles = this.ContextEntries.All(i => i.Entry.CanOpen);
            this.CanPreview = this.ContextEntries.Count == 1;
            this.PreviewIsOpen = this.ContextEntries.Any(i => i.Entry.CanEnumerateEntries);
            this.CanCopyPath = this.ContextEntries.Count >= 1;
            this.CanSave = this.ContextEntries.Count >= 1 && onlyFiles;
            this.CanReplace = this.ContextEntries.Count == 1 && onlyFiles;
        }

        public void LoadEntries(Entry directory)
        {
            this.cts.Cancel();
            this.cts = new();

            this.SelectedEntries.Clear();
            this.DisposeThumbnails();

            this.parentEntry = directory;
            this.Entries = [.. this.LoadEntries(directory.Path).Select(entry =>
            {
                var name = this.fsm.GetFileName(entry.Path) is { Length: > 0 } n ? n : entry.Path;
                return new EntryItem(entry, name, this.detector.Detect(entry));
            })];
        }

        private IList<Entry> LoadEntries(string path)
        {
            IList<Entry> entries;
            try
            {
                entries = [.. this.fsm.EnumerateEntries(path)];
            }
            catch (Exception ex)
            {
                this.logger.CouldNotEnumerateEntries(ex, path);
                entries = [];
            }

            return entries;
        }

        [RelayCommand]
        internal void CopyPath(Window owner)
        {
            var paths = string.Join(Environment.NewLine, this.ContextEntries.Select(e => e.Entry.Path));
            var clipboard = TopLevel.GetTopLevel(owner)?.Clipboard;
            clipboard?.SetTextAsync(paths);
        }

        [RelayCommand]
        internal void Preview()
        {
            EntryActivated?.Invoke(this, this.ContextEntries);
        }

        [RelayCommand]
        private async Task Export(Window owner)
        {
            var entries = this.ContextEntries;
            if (entries is [])
            {
                if (this.parentEntry == null)
                {
                    return;
                }
                else
                {
                    // TODO: We need to communicate that this is actually a root folder, even if it is an archive. This may be as simple as always using `EntryType.Folder`.
                    entries = [new(this.parentEntry, this.fsm.GetFileName(this.parentEntry.Path), this.parentEntry.CanOpen ? EntryType.Archive : EntryType.Folder)];
                }
            }

            var config = new ExportViewModel()
            {
                Entries = entries,
            };

            if (await new ExportDialog(config).ShowDialog<bool?>(owner).ConfigureAwait(true) == true)
            {
                var exportConfig = new FileExportService.ExportConfig
                {
                    Destination = config.Destination,
                    Include = config.IncludeFormat,
                    IncludeReferences = config.IncludeReferences,
                    AudioFormat = config.ConvertAudio ? config.AudioFormat : null,
                    ImageFormat = config.ConvertImages ? config.ImageFormat : null,
                    ModelFormat = config.ConvertModels ? config.ModelFormat : null,
                    Recursive = config.Recursive,
                    ArchiveDepth = config.RecurseArchives ? config.Depth : default(byte?),
                };

                using var progressViewModel = new ProgressViewModel();
                var progressView = new ProgressWindow
                {
                    DataContext = progressViewModel,
                };

                try
                {
                    var progressHandler = new Progress<FileExportService.ProgressReport>(progress =>
                    {
                        progressViewModel.Progress = progress.Discovered == 0 ? 0 : (float)(progress.Completed + progress.Faulted) / progress.Discovered;
                        progressViewModel.ProgressText = progress.Faulted == 0 ? $"Completed: {progress.Completed}" : $"Completed: {progress.Completed}, Failed: {progress.Faulted}";
                    });
                    progressView.Show(owner);
                    await this.fileSaveService.ExportAsync(owner, entries, exportConfig, progressViewModel.Cancel, progressHandler).ConfigureAwait(true);
                }
                finally
                {
                    progressView.Close();
                }
            }
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveAsync(Window owner)
        {
            if (this.ContextEntries is [EntryItem item])
            {
                await this.fileSaveService.SaveSingleAsync(owner, item).ConfigureAwait(false);
            }
            else
            {
                await this.fileSaveService.SaveMultipleAsync(owner, this.ContextEntries).ConfigureAwait(false);
            }
        }

        [RelayCommand(CanExecute = nameof(CanReplace))]
        private async Task ReplaceAsync(Window owner)
        {
            if (this.ContextEntries is [EntryItem item])
            {
                await this.fileSaveService.ReplaceSingleAsync(owner, item).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            this.DisposeThumbnails();
        }

        private void DisposeThumbnails()
        {
            this.thumbnails?.ForEach(t => t.Dispose());
            this.thumbnails = null;
        }
    }
}

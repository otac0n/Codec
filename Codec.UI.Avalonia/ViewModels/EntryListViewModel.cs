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
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;

    public sealed partial class EntryListViewModel : ObservableObject, IDisposable
    {
        private readonly NestedFileSystemManager fsm;
        private readonly EntryTypeDetector detector;
        private readonly FileSaveService fileSaveService;
        private readonly ImageLoader imageLoader;
        private CancellationTokenSource cts = new();

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
        [NotifyCanExecuteChangedFor(nameof(CopyPathCommand))]
        private bool canCopyPath;

        public event EventHandler<IList<EntryItem>>? EntryActivated;

        public EntryListViewModel(NestedFileSystemManager fsm, EntryTypeDetector detector, FileSaveService fileSaveService, ImageLoader imageLoader)
        {
            this.fsm = fsm;
            this.detector = detector;
            this.fileSaveService = fileSaveService;
            this.imageLoader = imageLoader;
            this.SelectedEntries.CollectionChanged += this.ContextChanged;
        }

        partial void OnContextEntryChanged(EntryItem? value)
        {
            this.ContextChanged(this, EventArgs.Empty);
        }

        private void ContextChanged(object? sender, EventArgs e)
        {
            this.CanPreview = this.ContextEntries.Count == 1;
            this.PreviewIsOpen = this.ContextEntries.Any(i => i.Entry.CanEnumerateEntries);
            this.CanCopyPath = this.ContextEntries.Count >= 1;
            this.CanSave = this.ContextEntries.Count >= 1 && this.ContextEntries.All(i => i.Entry.CanOpen);
        }

        public void LoadEntries(Entry directory)
        {
            this.cts.Cancel();
            this.cts = new();

            this.SelectedEntries.Clear();
            this.DisposeThumbnails();

            var entries = this.fsm.EnumerateEntries(directory.Path);

            this.Entries = [.. entries.Select(entry =>
            {
                var name = this.fsm.GetFileName(entry.Path) is { Length: > 0 } n ? n : entry.Path;
                return new EntryItem(entry, name, this.detector.Detect(entry));
            })];
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

namespace Codec.UI.Avalonia.ViewModels
{
    using System;
    using System.IO;
    using global::Avalonia.Media.Imaging;
    using Codec.Archives;
    using Codec.Files;
    using Codec.MGS;
    using Codec.UI.Avalonia.Models;
    using Codec.UI.Avalonia.Services;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using Entry = Codec.Archives.NestedFileSystemManager.Entry;
    using EntryType = Codec.Services.EntryTypeDetector.EntryType;

    public partial class BrowserViewModel : ObservableObject
    {
        private readonly IServiceProvider serviceProvider;
        private readonly NestedFileSystemManager fsm;
        private readonly ImageLoader imageLoader;
        private bool navigating;

        public FileTreeViewModel Tree { get; }

        public EntryListViewModel List { get; }

        [ObservableProperty]
        private bool canGoUp = false;

        [ObservableProperty]
        private string currentPath = string.Empty;

        [ObservableProperty]
        private string? statusMessage;

        [ObservableProperty]
        private ViewMode currentViewMode = ViewMode.List;

        public BrowserViewModel(
            IServiceProvider serviceProvider,
            NestedFileSystemManager fsm,
            FileTreeViewModel fileTreeViewModel,
            ImageLoader imageLoader,
            EntryListViewModel entryListViewModel,
            EnvironmentOptions env)
        {
            this.serviceProvider = serviceProvider;
            this.fsm = fsm;
            this.imageLoader = imageLoader;
            this.currentPath = Path.Combine(
                env.SteamApps,
                WellKnownPaths.AllDataBin,
                WellKnownPaths.CD1Path,
                WellKnownPaths.StageDirPath);
            this.Tree = fileTreeViewModel;
            this.List = entryListViewModel;

            entryListViewModel.EntryActivated += this.OnEntryActivated;
            fileTreeViewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(FileTreeViewModel.SelectedNode) &&
                    this.Tree.SelectedNode is { } node && !this.navigating)
                {
                    this.NavigateToEntry(node.Entry);
                }
            };

            this.CommitPathBox();
        }

        [RelayCommand]
        private void CommitPathBox()
        {
            this.Navigate(this.CurrentPath);
        }

        [RelayCommand]
        private void GoUp()
        {
            this.Navigate(PathExtensions.GetDirectoryName(this.CurrentPath));
        }

        private void Navigate(string path)
        {
            if (this.fsm.TryGetEntry(path, out var entry))
            {
                this.NavigateToEntry(entry);
            }
        }

        private void NavigateToEntry(Entry entry)
        {
            if (this.navigating)
            {
                return;
            }

            this.navigating = true;
            try
            {
                this.CurrentPath = entry.Path;
                this.CanGoUp = entry.Path?.IndexOfAny(PathExtensions.Separators) > -1;
                this.Tree.SelectEntry(entry);
                this.List.LoadEntries(entry);
            }
            finally
            {
                this.navigating = false;
            }
        }

        private async void OnEntryActivated(object? sender, EntryItem item)
        {
            if (item.Entry.CanEnumerateEntries)
            {
                this.NavigateToEntry(item.Entry);
                return;
            }

            switch (item.EntryType)
            {
                case EntryType.Audio:
                    {
                        var audioStream = this.fsm.Resolve<AudioStream>(item.Entry.Path) ?? (AudioStream)this.fsm.OpenRead(item.Entry.Path);
                        this.AudioPreviewRequested?.Invoke(this, new(audioStream, item.Entry.Path, this.fsm));
                    }
                    break;
                case EntryType.Image:
                    try
                    {
                        var bmp = await this.imageLoader.LoadAsync(item.Entry).ConfigureAwait(true);
                        if (bmp != null)
                        {
                            this.ImagePreviewRequested?.Invoke(this, new(bmp, item.Entry.Path, this.fsm));
                        }
                    }
                    catch (Exception ex)
                    {
                        this.StatusMessage = $"Failed to load image: {ex.Message}";
                    }
                    break;
                case EntryType.Model:
                    {
                        if (this.fsm.Resolve<RenderableScene>(item.Entry.Path) is RenderableScene model)
                        {
                            this.ModelPreviewRequested?.Invoke(this, new(model, item.Entry.Path, this.fsm));
                        }
                    }
                    break;
            }
        }

        public event EventHandler<PreviewRequestedEventArgs<AudioStream>>? AudioPreviewRequested;

        public event EventHandler<PreviewRequestedEventArgs<Bitmap>>? ImagePreviewRequested;

        public event EventHandler<PreviewRequestedEventArgs<RenderableScene>>? ModelPreviewRequested;

        public class PreviewRequestedEventArgs<T>(T item, string path, NestedFileSystemManager parent) : EventArgs
        {
            public T Item { get; } = item;

            public string Path { get; } = path;

            public NestedFileSystemManager Parent { get; } = parent;
        }
    }
}

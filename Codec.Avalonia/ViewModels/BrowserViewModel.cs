namespace Codec.Avalonia.ViewModels
{
    using System;
    using System.IO;
    using Codec.Archives;
    using Codec.Avalonia.Models;
    using Codec.Avalonia.Services;
    using Codec.Files;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using global::Avalonia.Media.Imaging;
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
            if (this.fsm.TryGetEntry(this.CurrentPath, out var entry))
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
                        this.AudioPreviewRequested?.Invoke(this, (this.fsm.GetFileName(item.Entry.Path), audioStream));
                    }
                    break;

                case EntryType.Image:
                    try
                    {
                        var bmp = await this.imageLoader.LoadAsync(item.Entry).ConfigureAwait(true);
                        if (bmp != null)
                        {
                            this.ImagePreviewRequested?.Invoke(this, (this.fsm.GetFileName(item.Entry.Path), bmp));
                        }
                    }
                    catch (Exception ex)
                    {
                        this.StatusMessage = $"Failed to load image: {ex.Message}";
                    }

                    break;
            }
        }

        public event EventHandler<(string FileName, AudioStream Stream)>? AudioPreviewRequested;

        public event EventHandler<(string FileName, Bitmap Bitmap)>? ImagePreviewRequested;
    }
}

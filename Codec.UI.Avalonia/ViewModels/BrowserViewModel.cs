namespace Codec.UI.Avalonia.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using global::Avalonia.Media.Imaging;
    using Codec.Archives;
    using Codec.Files;
    using Codec.MGS;
    using Codec.Services;
    using Codec.UI.Avalonia.Models;
    using Codec.UI.Avalonia.Services;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using Microsoft.Extensions.Logging;

    public partial class BrowserViewModel : ObservableObject
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<BrowserViewModel> logger;
        private readonly NestedFileSystemManager fsm;
        private readonly ImageLoader imageLoader;
        private readonly List<Entry> history = [];
        private int historyIndex = -1;
        private bool navigating;

        public FileTreeViewModel Tree { get; }

        public EntryListViewModel List { get; }

        [ObservableProperty]
        private bool canGoBack = false;

        [ObservableProperty]
        private bool canGoForward = false;

        [ObservableProperty]
        private bool canGoUp = false;

        [ObservableProperty]
        private string currentPath = string.Empty;

        public ObservableCollection<NotifyingLoggerProvider.LogEntry> Errors { get; } = [];

        [ObservableProperty]
        private ViewMode currentViewMode = ViewMode.List;

        [ObservableProperty]
        private bool showErrors = true;

        public BrowserViewModel(
            IServiceProvider serviceProvider,
            ILogger<BrowserViewModel> logger,
            NestedFileSystemManager fsm,
            FileTreeViewModel fileTreeViewModel,
            ImageLoader imageLoader,
            EntryListViewModel entryListViewModel)
        {
            this.serviceProvider = serviceProvider;
            this.logger = logger;
            this.fsm = fsm;
            this.imageLoader = imageLoader;
            this.currentPath = WellKnownPaths.StartPaths.FirstOrDefault(Directory.Exists) ?? string.Empty;
            this.Tree = fileTreeViewModel;
            this.List = entryListViewModel;

            entryListViewModel.EntryActivated += this.OnEntryActivated;
            fileTreeViewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(FileTreeViewModel.SelectedNode) &&
                    this.Tree.SelectedNode is { } node && !this.navigating)
                {
                    this.Navigate(node.Entry);
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
        private void GoBack()
        {
            this.historyIndex--;
            this.Navigate(this.history[this.historyIndex], addHistory: false);
        }

        [RelayCommand]
        private void GoForward()
        {
            this.historyIndex++;
            this.Navigate(this.history[this.historyIndex], addHistory: false);
        }

        [RelayCommand]
        private void GoUp()
        {
            this.Navigate(PathExtensions.GetDirectoryName(this.CurrentPath));
        }

        [RelayCommand]
        private void ToggleErrors()
        {
            this.ShowErrors = !this.ShowErrors;
        }

        private void Navigate(string path)
        {
            if (this.fsm.TryGetEntry(path, out var entry))
            {
                this.Navigate(entry);
            }
        }

        private void Navigate(Entry entry, bool addHistory = true)
        {
            if (this.navigating)
            {
                return;
            }

            this.navigating = true;
            try
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

                this.CurrentPath = entry.Path;
                this.CanGoUp = entry.Path?.IndexOfAny(PathExtensions.Separators) > -1;
                this.CanGoBack = this.historyIndex > 0;
                this.CanGoForward = this.historyIndex < this.history.Count - 1;
                this.Tree.SelectEntry(entry);
                this.List.LoadEntries(entry);
            }
            finally
            {
                this.navigating = false;
            }
        }

        private async void OnEntryActivated(object? sender, IList<EntryItem> items)
        {
            if (items is not [var item])
            {
                return;
            }

            if (item.Entry.CanEnumerateEntries)
            {
                this.Navigate(item.Entry);
                return;
            }

            try
            {
                switch (item.EntryType)
                {
                    case EntryType.Audio:
                        {
                            if (this.fsm.Resolve<AudioStream>(item.Entry.Path) is AudioStream audioStream)
                            {
                                this.AudioPreviewRequested?.Invoke(this, new(audioStream, item.Entry.Path, this.fsm));
                            }
                        }

                        break;

                    case EntryType.Image:
                        {
                            var bmp = await this.imageLoader.LoadAsync(item.Entry).ConfigureAwait(true);
                            if (bmp != null)
                            {
                                this.ImagePreviewRequested?.Invoke(this, new(bmp, item.Entry.Path, this.fsm));
                            }
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
            catch (Exception ex)
            {
                this.logger.FailedToLoad(ex, item.Entry.Path);
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

namespace Codec.UI.Avalonia.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using Codec.Archives;
    using Codec.Services;
    using CommunityToolkit.Mvvm.ComponentModel;
    using Microsoft.Extensions.Logging;

    public sealed partial class FileTreeNodeViewModel : ObservableObject
    {
        private static readonly FileTreeNodeViewModel Placeholder = new("...");
        private readonly NestedFileSystemManager fsm;
        private readonly EntryTypeDetector detector;
        private readonly ILogger<FileTreeViewModel> logger;
        private bool childrenLoaded;

        [ObservableProperty] private bool isExpanded;

#pragma warning disable CS8618
        public FileTreeNodeViewModel(string displayName)
        {
            this.DisplayName = displayName;
        }
#pragma warning restore CS8618

        public FileTreeNodeViewModel(Entry entry, string displayName, NestedFileSystemManager fsm, EntryTypeDetector detector, ILogger<FileTreeViewModel> logger)
        {
            this.Entry = entry;
            this.DisplayName = displayName;
            this.fsm = fsm;
            this.detector = detector;
            this.logger = logger;
            if (entry is { CanEnumerateEntries: true })
            {
                this.Children.Add(Placeholder);
            }
        }

        public string DisplayName { get; }

        public Entry Entry { get; }

        public ObservableCollection<FileTreeNodeViewModel> Children { get; } = [];

        partial void OnIsExpandedChanging(bool value)
        {
            if (value)
            {
                if (this.childrenLoaded)
                {
                    return;
                }

                this.childrenLoaded = true;

                this.Children.Clear();

                var childEntries = this.LoadEntries(this.Entry.Path).ToList();
                foreach (var child in childEntries.Where(e => e.CanEnumerateEntries))
                {
                    var name = this.fsm.GetFileName(child.Path) switch { "" => child.Path, var x => x };
                    this.Children.Add(new FileTreeNodeViewModel(child, name, this.fsm, this.detector, this.logger));
                }
            }
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
    }
}

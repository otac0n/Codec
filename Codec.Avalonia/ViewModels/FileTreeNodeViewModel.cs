namespace Codec.Avalonia.ViewModels
{
    using System.Collections.ObjectModel;
    using System.Linq;
    using Codec.Archives;
    using Codec.Services;
    using CommunityToolkit.Mvvm.ComponentModel;
    using Entry = Codec.Archives.NestedFileSystemManager.Entry;

    public sealed partial class FileTreeNodeViewModel : ObservableObject
    {
        private static readonly FileTreeNodeViewModel Placeholder = new("...");
        private readonly NestedFileSystemManager fsm;
        private readonly EntryTypeDetector detector;
        private bool childrenLoaded;

        [ObservableProperty] private bool isExpanded;

#pragma warning disable CS8618
        public FileTreeNodeViewModel(string displayName)
        {
            this.DisplayName = displayName;
        }
#pragma warning restore CS8618

        public FileTreeNodeViewModel(Entry entry, string displayName, NestedFileSystemManager fsm, EntryTypeDetector detector)
        {
            this.Entry = entry;
            this.DisplayName = displayName;
            this.fsm = fsm;
            this.detector = detector;

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

                var childEntries = this.fsm.EnumerateEntries(this.Entry.Path).Where(e => e.CanEnumerateEntries);
                foreach (var child in childEntries)
                {
                    var name = this.fsm.GetFileName(child.Path) switch { "" => child.Path, var x => x };
                    this.Children.Add(new FileTreeNodeViewModel(child, name, this.fsm, this.detector));
                }
            }
        }
    }
}

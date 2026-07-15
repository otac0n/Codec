namespace Codec.UI.Avalonia.ViewModels
{
    using System.Collections.ObjectModel;
    using System.Linq;
    using Codec.Archives;
    using Codec.Services;
    using CommunityToolkit.Mvvm.ComponentModel;
    using Microsoft.Extensions.Logging;

    public sealed partial class FileTreeViewModel : ObservableObject
    {
        private readonly FileTreeNodeViewModel rootNodeViewModel;

        public ObservableCollection<FileTreeNodeViewModel> RootNodes { get; }

        [ObservableProperty]
        private FileTreeNodeViewModel? selectedNode;

        public FileTreeViewModel(NestedFileSystemManager fsm, EntryTypeDetector detector, ILogger<FileTreeViewModel> logger)
        {
            var root = fsm.RootEntry;
            this.rootNodeViewModel = new FileTreeNodeViewModel(root, "(root)", fsm, detector, logger);
            this.RootNodes = [this.rootNodeViewModel];
        }

        public void SelectEntry(Entry entry)
        {
            var segments = PathExtensions.SplitPath(entry.Path).ToList();
            var current = this.rootNodeViewModel;

            foreach (var segment in segments)
            {
                current.IsExpanded = true;

                static string GetName(string path)
                {
                    var name = PathExtensions.GetFileName(path);
                    return string.IsNullOrEmpty(name) ? path : name;
                }

                var next = current.Children
                    .FirstOrDefault(n => GetName(n.Entry.Path) == segment);

                if (next is null)
                {
                    break;
                }

                current = next;
            }

            if (current is not null)
            {
                this.SelectedNode = current;
            }
        }
    }
}

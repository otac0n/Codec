namespace Codec.UI.Avalonia.ViewModels
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using global::Avalonia.Controls;
    using global::Avalonia.Platform.Storage;
    using Codec.Services;
    using Codec.UI.Avalonia.Models;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;

    public sealed partial class ExportViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AnyFolders))]
        [NotifyPropertyChangedFor(nameof(AnyAudio))]
        [NotifyPropertyChangedFor(nameof(AnyImages))]
        [NotifyPropertyChangedFor(nameof(AnyModels))]
        public partial IList<EntryItem> Entries { get; set; }

        public bool AnyFolders => this.Entries.Any(e => e.Entry.CanEnumerateEntries);

        public bool AnyAudio => this.AnyFolders || this.Entries.Any(e => e.EntryType == EntryType.Audio);

        public bool AnyImages => this.AnyFolders || this.Entries.Any(e => e.EntryType == EntryType.Image);

        public bool AnyModels => this.AnyFolders || this.Entries.Any(e => e.EntryType == EntryType.Model);

        public bool Valid => !string.IsNullOrWhiteSpace(this.Destination) && Path.IsPathFullyQualified(this.Destination);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Valid))]
        public partial string Destination { get; set; } = "";

        [ObservableProperty]
        public partial string IncludeFormat { get; set; } = "*.*";

        [ObservableProperty]
        public partial bool IncludeReferences { get; set; } = true;

        [ObservableProperty]
        public partial bool ConvertAudio { get; set; }

        [ObservableProperty]
        public partial bool ConvertImages { get; set; }

        [ObservableProperty]
        public partial bool ConvertModels { get; set; }

        [ObservableProperty]
        public partial string AudioFormat { get; set; } = "wav";

        [ObservableProperty]
        public partial string ImageFormat { get; set; } = "png";

        [ObservableProperty]
        public partial string ModelFormat { get; set; } = "glb";

        [ObservableProperty]
        public partial bool Recursive { get; set; } = true;

        [ObservableProperty]
        public partial bool RecurseArchives { get; set; } = true;

        [ObservableProperty]
        public partial byte Depth { get; set; } = 10;

        [RelayCommand]
        private async Task BrowseAsync(Window owner)
        {
            var options = new FolderPickerOpenOptions
            {
                Title = "Choose Export Folder",
            };
            var folders = await owner.StorageProvider.OpenFolderPickerAsync(options).ConfigureAwait(true);
            this.Destination = folders is [var folder] ? folder.Path.LocalPath : string.Empty;
        }
    }
}

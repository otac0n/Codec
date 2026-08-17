namespace Codec.UI.Avalonia.Services
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using global::Avalonia.Controls;
    using global::Avalonia.Platform.Storage;
    using Codec.Services;
    using Codec.UI.Avalonia.Models;
    using Codec.UI.Avalonia.Views;

    public sealed class FileSaveService(FileExportService exportService)
    {
        public async Task ExportAsync(Window owner, IList<EntryItem> entries, FileExportService.ExportConfig config = null)
        {
            config ??= new();
            await exportService.ExportAsync([.. entries.Select(e => (e.Entry, e.EntryType))], config).ConfigureAwait(false);
        }

        public async Task SaveSingleAsync(Window owner, EntryItem item)
        {
            await exportService.SaveSingleAsync((item.Entry, item.EntryType), async (suggestedFileName, type, supportedPatterns) =>
            {
                var allFiles = new FilePickerFileType("All Files") { Patterns = ["*.*"] };
                var options = new FilePickerSaveOptions
                {
                    Title = "Save File",
                    SuggestedFileName = suggestedFileName,
                    FileTypeChoices = supportedPatterns is string supportedTypes
                        ? [new FilePickerFileType($"{type} Files") { Patterns = supportedTypes.Split(';') }, allFiles]
                        : [allFiles],
                };

                var file = await owner.StorageProvider.SaveFilePickerAsync(options).ConfigureAwait(false);
                return file?.Path.LocalPath;
            }).ConfigureAwait(false);
        }

        public async Task SaveMultipleAsync(Window owner, IEnumerable<EntryItem> selectedItems)
        {
            await exportService.SaveMultipleAsync(
                selectedItems.Select(e => e.Entry),
                async () =>
                {
                    var options = new FolderPickerOpenOptions
                    {
                        Title = "Save to Folder",
                    };
                    var folders = await owner.StorageProvider.OpenFolderPickerAsync(options).ConfigureAwait(true);
                    return folders is [var folder] ? folder.Path.LocalPath : null;
                },
                async _ => await new ConfirmOverwriteDialog().ShowDialog<bool?>(owner).ConfigureAwait(false) == true
            ).ConfigureAwait(false);
        }

        public async Task ReplaceSingleAsync(Window owner, EntryItem item)
        {
            await exportService.ReplaceSingleAsync(item.Entry, async (suggestedFileName, type, supportedPatterns) =>
            {
                var allFiles = new FilePickerFileType("All Files") { Patterns = ["*.*"] };
                var options = new FilePickerOpenOptions
                {
                    Title = "Open File",
                    AllowMultiple = false,
                    FileTypeFilter = supportedPatterns is string supportedTypes
                        ? [new FilePickerFileType($"{type} Files") { Patterns = supportedTypes.Split(';') }, allFiles]
                        : [allFiles],
                };

                var files = await owner.StorageProvider.OpenFilePickerAsync(options).ConfigureAwait(false);
                return files?.SingleOrDefault()?.Path.LocalPath;
            }).ConfigureAwait(false);
        }
    }
}

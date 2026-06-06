namespace Codec.Avalonia.Services
{
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using global::Avalonia.Controls;
    using global::Avalonia.Platform.Storage;
    using Codec.Archives;
    using Codec.Avalonia.Models;
    using Codec.Avalonia.Views;
    using ImageMagick;

    public sealed class FileSaveService(NestedFileSystemManager fsm)
    {
        public async Task SaveSingleAsync(Window owner, EntryItem item)
        {
            var entry = item.Entry;
            if (!fsm.FileExists(entry.Path))
            {
                return;
            }

            MagickImageInfo? fileInfo = null;
            try
            {
                using var input = fsm.OpenRead(entry.Path);
                fileInfo = new MagickImageInfo(input);
            }
            catch (MagickMissingDelegateErrorException)
            {
            }

            var allFiles = new FilePickerFileType("All Files") { Patterns = ["*.*"] };
            var options = new FilePickerSaveOptions
            {
                Title = "Save File",
                SuggestedFileName = fsm.GetFileName(entry.Path),
                FileTypeChoices = fileInfo != null
                    ? [new FilePickerFileType("Image Files") { Patterns = ["*.bmp", "*.gif", "*.jpg", "*.jpeg", "*.png", "*.tif", "*.tiff", "*.pcx"] }, allFiles]
                    : [allFiles],
            };

            var file = await owner.StorageProvider.SaveFilePickerAsync(options).ConfigureAwait(false);
            if (file is null)
            {
                return;
            }

            {
                using var input = fsm.OpenRead(entry.Path);
                var path = file.Path.LocalPath;
                if (Path.GetExtension(path) != fsm.GetExtension(entry.Path))
                {
                    if (fsm.Resolve<Bitmap>(entry.Path) is var resolved)
                    {
                        resolved.Save(path);
                        return;
                    }
                }

                using var output = File.Create(path);
                input.CopyTo(output);
            }
        }

        public async Task SaveMultipleAsync(Window owner, IEnumerable<EntryItem> selectedItems)
        {
            var entries = selectedItems.Select(e => e.Entry);

            var options = new FolderPickerOpenOptions
            {
                Title = "Save to Folder",
            };
            var folders = await owner.StorageProvider.OpenFolderPickerAsync(options).ConfigureAwait(true);
            if (folders is not [var folder])
            {
                return;
            }

            var path = folder.Path.LocalPath;
            var targetFiles = entries.Select(e => (Source: e.Path, Target: Path.Combine(path, Path.GetFileName(e.Path)))).ToList();
            if (targetFiles.Any(t => File.Exists(t.Target)))
            {
                var confirmed = await ConfirmOverwriteAsync(owner).ConfigureAwait(false);
                if (!confirmed)
                {
                    return;
                }
            }

            foreach (var (source, target) in targetFiles)
            {
                using var input = fsm.OpenRead(source);
                using var output = File.Create(target);
                await input.CopyToAsync(output).ConfigureAwait(false);
            }
        }

        private static async Task<bool> ConfirmOverwriteAsync(Window owner) =>
            await new ConfirmOverwriteDialog().ShowDialog<bool?>(owner).ConfigureAwait(false) == true;
    }
}

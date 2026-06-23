namespace Codec.UI.Avalonia.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Assimp;
    using global::Avalonia.Controls;
    using global::Avalonia.Platform.Storage;
    using Codec.Archives;
    using Codec.Files;
    using Codec.Services;
    using Codec.UI.Avalonia.Models;
    using Codec.UI.Avalonia.Views;
    using ImageMagick;

    public sealed class FileSaveService(NestedFileSystemManager fsm, EntryTypeDetector detector)
    {
        public async Task SaveSingleAsync(Window owner, EntryItem item)
        {
            var entry = item.Entry;
            if (!fsm.FileExists(entry.Path))
            {
                return;
            }

            var type = detector.Detect(entry);

            var allFiles = new FilePickerFileType("All Files") { Patterns = ["*.*"] };
            var options = new FilePickerSaveOptions
            {
                Title = "Save File",
                SuggestedFileName = fsm.GetFileName(entry.Path),
                FileTypeChoices = detector[type] is string supportedTypes
                    ? [new FilePickerFileType($"{type} Files") { Patterns = supportedTypes.Split(';') }, allFiles]
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
                if (!string.Equals(Path.GetExtension(path), fsm.GetExtension(entry.Path), StringComparison.OrdinalIgnoreCase))
                {
                    switch (type)
                    {
                        case EntryTypeDetector.EntryType.Image:
                            if (fsm.Resolve<MagickImage>(entry.Path) is MagickImage image)
                            {
                                image.Write(path);
                                return;
                            }
                            break;
                        case EntryTypeDetector.EntryType.Model:
                            if (fsm.Resolve<RenderableScene>(entry.Path) is RenderableScene scene)
                            {
                                // TODO: Handle linked image exports & renames.
                                var context = new AssimpContext();
                                var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
                                var formatId = context.GetSupportedExportFormats()
                                    .FirstOrDefault(f => f.FileExtension.Equals(ext, StringComparison.OrdinalIgnoreCase))
                                    ?.FormatId;
                                if (formatId != null)
                                {
                                    context.ExportFile(scene.Scene, path, formatId);
                                    return;
                                }
                            }
                            break;
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

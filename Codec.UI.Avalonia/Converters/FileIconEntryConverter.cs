namespace Codec.UI.Avalonia.Converters
{
    using System;
    using System.Globalization;
    using global::Avalonia.Data.Converters;
    using IconPacks.Avalonia.FontAwesome;
    using EntryType = Codec.Services.EntryTypeDetector.EntryType;

    public sealed class FileIconEntryConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value switch
            {
                EntryType.Folder => PackIconFontAwesomeKind.FolderOpenSolid,
                EntryType.Archive => PackIconFontAwesomeKind.BoxArchiveSolid,
                EntryType.Image => PackIconFontAwesomeKind.FileImageSolid,
                EntryType.Audio => PackIconFontAwesomeKind.FileAudioSolid,
                EntryType.Video => PackIconFontAwesomeKind.FileVideoSolid,
                EntryType.Model => PackIconFontAwesomeKind.ShapesSolid,
                _ => PackIconFontAwesomeKind.FileSolid,
            };

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}

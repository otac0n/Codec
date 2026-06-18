namespace Codec.UI.Avalonia.Models
{
    using Codec.Archives;
    using Codec.Services;

    public sealed record EntryItem(
        NestedFileSystemManager.Entry Entry,
        string DisplayName,
        EntryTypeDetector.EntryType EntryType);
}

namespace Codec.UI.Avalonia.Models
{
    using Codec.Archives;
    using Codec.Services;

    public sealed record EntryItem(
        Entry Entry,
        string DisplayName,
        EntryType EntryType);
}

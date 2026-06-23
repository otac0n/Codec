namespace Codec.Services
{
    using System;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Codec.Archives;
    using Microsoft.Extensions.DependencyInjection;

    public sealed class EntryTypeDetector
    {
        private readonly NestedFileSystemManager fsm;
        private readonly ImmutableList<(Regex Regex, EntryType EntryType)> matchers;
        private readonly ImmutableDictionary<EntryType, string> typeChoices;

        public EntryTypeDetector(NestedFileSystemManager fsm, IServiceProvider serviceProvider)
        {
            this.fsm = fsm;
            var matchers = serviceProvider.GetServices<EntryTypeMatcher>();
            this.matchers = [.. matchers.Select(m => (PathExtensions.GlobToRegex(m.GlobPattern), m.EntryType))];
            this.typeChoices = (from m in serviceProvider.GetServices<EntryTypeMatcher>()
                                group m.GlobPattern by m.EntryType into g
                                select (g.Key, Pattern: string.Join(";", g))).ToImmutableDictionary(g => g.Key, g => g.Pattern);
        }

        public string? this[EntryType entryType] => this.typeChoices.TryGetValue(entryType, out var type) ? type : null;

        public EntryType Detect(Entry entry)
        {
            if (entry.CanEnumerateEntries && !entry.CanOpen)
            {
                return EntryType.Folder;
            }

            if (entry.CanEnumerateEntries)
            {
                return EntryType.Archive;
            }

            foreach (var matcher in this.matchers)
            {
                if (matcher.Regex.IsMatch(this.fsm.GetFileName(entry.Path)))
                {
                    return matcher.EntryType;
                }
            }

            return EntryType.File;
        }
    }
}

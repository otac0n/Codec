// Copyright © John Gietzen. All Rights Reserved. This source is subject to the GPL license. Please see license.md for more information.

namespace Codec
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO.Abstractions;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    public static partial class PathExtensions
    {
        public static readonly char[] Separators = ['/', '\\'];

        [GeneratedRegex(@"(?<=(?<![/\\])[/\\])[/\\]*(?!$)")]
        private static partial Regex SegmentSplitRegex();

        public static string CombineIgnoringAbsolute(this IPath path, string prefix, string suffix) =>
            path.Combine(prefix.TrimEnd(Separators), suffix.TrimStart(Separators));

        public static string CombineWithSeparator(this IPath path, char separator, string prefix, string suffix)
        {
            if (string.IsNullOrEmpty(prefix) || path.IsPathRooted(suffix))
            {
                return suffix ?? string.Empty;
            }

            if (string.IsNullOrEmpty(suffix))
            {
                return prefix ?? string.Empty;
            }

            return prefix.TrimEnd(Separators) + separator + suffix.TrimStart(Separators);
        }

        public static string GetRelativePath(this IPath path, char separator, string relativeTo, string destination)
        {
            ArgumentException.ThrowIfNullOrEmpty(relativeTo, nameof(relativeTo));
            ArgumentException.ThrowIfNullOrEmpty(destination, nameof(destination));

            if (path.IsPathRooted(relativeTo) != path.IsPathRooted(destination))
            {
                return destination;
            }

            var from = SegmentSplitRegex().Split(relativeTo);
            var to = SegmentSplitRegex().Split(destination);

            var common = 0;
            var max = Math.Min(from.Length, to.Length);

            while (common < max && SegmentEquals(from[common], to[common], StringComparison.Ordinal))
            {
                common++;
            }

            if (common == from.Length && common == to.Length)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            if (common < from.Length)
            {
                for (var i = from.Length - 1; i >= common; i--)
                {
                    sb.Append("..");
                    if (i >= 1)
                    {
                        sb.Append(from[i - 1][^1]);
                    }
                    else
                    {
                        sb.Append(separator);
                    }
                }
            }

            for (var i = common; i < to.Length; i++)
            {
                sb.Append(to[i]);
            }

            return sb.Length == 0 ? string.Empty : sb.ToString();
        }

        private static bool SegmentEquals(string a, string b, StringComparison comparison)
        {
            var aSpan = a[^1] is '/' or '\\' ? a.AsSpan()[..^1] : a.AsSpan();
            var bSpan = b[^1] is '/' or '\\' ? b.AsSpan()[..^1] : b.AsSpan();
            return aSpan.Equals(bSpan, comparison);
        }

        public static string[] Split(string path) => path.Split(Separators, StringSplitOptions.RemoveEmptyEntries);

        public static bool IsPathRooted(ReadOnlySpan<char> path) => System.IO.Path.IsPathRooted(path);

        public static bool IsPathRooted(string? path) => System.IO.Path.IsPathRooted(path);

        public static ReadOnlySpan<char> GetExtension(ReadOnlySpan<char> path) => System.IO.Path.GetExtension(path);

        public static string? GetExtension(string? path) => System.IO.Path.GetExtension(path);

        public static ReadOnlySpan<char> GetFileNameWithoutExtension(ReadOnlySpan<char> path) => System.IO.Path.GetFileNameWithoutExtension(path);

        public static string? GetFileNameWithoutExtension(string? path) => System.IO.Path.GetFileNameWithoutExtension(path);

        internal static ReadOnlySpan<char> GetPathRoot(ReadOnlySpan<char> path)
        {
            // TODO:
            throw new NotImplementedException();
        }

        [return: NotNullIfNotNull(nameof(path))]
        public static string? GetPathRoot(string? path)
        {
            if (path == null)
            {
                return null;
            }

            var i = path.IndexOfAny(Separators);
            if (i == -1)
            {
                return path.Length > 0 && path[^1] == ':' ? path : string.Empty;
            }

            if (i == 0)
            {
                return path[..1];
            }

            return path[i - 1] == ':' ? path[..(i + 1)] : string.Empty;
        }

        public static ReadOnlySpan<char> GetDirectoryName(ReadOnlySpan<char> path)
        {
            // TODO:
            throw new NotImplementedException();
        }

        [return: NotNullIfNotNull(nameof(path))]
        public static string? GetDirectoryName(string? path)
        {
            if (path == null)
            {
                return null;
            }

            var i = path.LastIndexOfAny(Separators);
            if (i < 0)
            {
                return string.Empty;
            }

            if (i == 0 || path[i - 1] == ':')
            {
                return path.Length == (i + 1) ? string.Empty : path[..(i + 1)];
            }

            return path[..i];
        }

        public static ReadOnlySpan<char> GetFileName(ReadOnlySpan<char> path)
        {
            // TODO:
            throw new NotImplementedException();
        }

        [return: NotNullIfNotNull(nameof(path))]
        public static string? GetFileName(string? path)
        {
            if (path == null)
            {
                return null;
            }

            var i = path.LastIndexOfAny(Separators);
            return i >= 0 ? path[(i + 1)..] : path.Length > 0 && path[^1] == ':' ? string.Empty : path;
        }

        [return: NotNullIfNotNull(nameof(path))]
        public static string? ChangeExtension(string? path, string? extension)
        {
            return System.IO.Path.ChangeExtension(path, extension);
        }

        public static IEnumerable<string> SplitPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                yield return string.Empty;
                yield break;
            }

            var root = PathExtensions.GetPathRoot(path);
            if (!string.IsNullOrEmpty(root))
            {
                yield return root;
            }

            var ix = root?.Length ?? 0;
            while (ix < path.Length)
            {
                var sep = path.IndexOfAny(PathExtensions.Separators, ix);
                if (sep < 0)
                {
                    yield return path[ix..];
                    break;
                }

                if (sep > 0)
                {
                    yield return path[ix..sep];
                }

                ix = sep + 1;
            }
        }

        public static Regex GlobToRegex(string searchPattern) =>
            new Regex(
                "^" +
                string.Concat(
                    Regex.Split(searchPattern, @"(\?|\*+)")
                        .Select(p =>
                            p == ""  ? "" :
                            p[0] == '?' ? "." :
                            p[0] == '*' ? ".*" :
                            Regex.Escape(p))) +
                "$",
                RegexOptions.Singleline);

        public static bool PrefixMatch(string[] prefix, string[] subject)
        {
            if (prefix.Length > subject.Length)
            {
                return false;
            }

            for (var i = 0; i < prefix.Length; i++)
            {
                if (prefix[i] != subject[i])
                {
                    return false;
                }
            }

            return true;
        }

        public static bool EndsWithSlash(string? path) => path != null && path.Length > 0 && path.LastIndexOfAny(Separators) == path.Length - 1;
    }
}

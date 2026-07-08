// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.Archives
{
    using System.IO;
    using System.IO.Abstractions;

    /// <summary>
    /// A wrapper that holds a file share handle.
    /// </summary>
    /// <param name="stream">The stream with a filename.</param>
    /// <param name="path">The path from which the file can (at one point) be acquired.</param>
    /// <param name="isAsync">A value indicating whether or not the underlying stream was opened in an async mode.</param>
    public class StreamWrapper(Stream stream, string path, bool isAsync)
        : FileSystemStream(stream, path, isAsync)
    {
    }
}

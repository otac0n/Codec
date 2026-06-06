// Copyright © John Gietzen. All Rights Reserved. This source is subject to the GPL license. Please see license.md for more information.

namespace Codec.Archives
{
    using System;
    using System.IO;
    using System.IO.Abstractions;

    /// <summary>
    /// A wrapper that holds a file share handle.
    /// </summary>
    /// <param name="stream">The stream with a filename.</param>
    /// <param name="path">The path from which the file can (at one point) be acquired.</param>
    /// <param name="handle">The file share handle.</param>
    /// <param name="isAsync">A value indicating whether or not the underlying stream was opened in an async mode.</param>
    internal class StreamWrapper(Stream stream, string path, IDisposable handle, bool isAsync)
        : FileSystemStream(stream, path, isAsync)
    {
        /// <inheritdoc/>
        protected override void Dispose(bool disposing) => handle.Dispose();
    }
}

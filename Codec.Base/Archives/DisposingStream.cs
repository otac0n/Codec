// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.Archives
{
    using System;
    using System.IO;

    /// <summary>
    /// A wrapper that holds an OnClose (disposable) handle.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="DisposingStream"/> class.
    /// </remarks>
    /// <param name="stream">The stream with a filename.</param>
    /// <param name="onClose">The action to perform when the stream is closed.</param>
    public class DisposingStream(Stream stream, Action<Stream> onClose) : WrappedStreamBase(stream)
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DisposingStream"/> class.
        /// </summary>
        /// <param name="stream">The stream with a filename.</param>
        /// <param name="handle">The object to dispose when the stream is closed.</param>
        public DisposingStream(Stream stream, IDisposable handle)
            : this(stream, _ => handle.Dispose())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DisposingStream"/> class.
        /// </summary>
        /// <param name="stream">The stream with a filename.</param>
        /// <param name="onClose">The action to perform when the stream is closed.</param>
        public DisposingStream(Stream stream, Action onClose)
            : this(stream, _ => onClose())
        {
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            onClose(this.inner);
            base.Dispose(disposing);
        }
    }
}

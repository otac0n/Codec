// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.UI
{
    using System;
    using Microsoft.Extensions.Logging;

    public static partial class LoggingMessages
    {
        [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load '{Path}'")]
        public static partial void FailedToLoad(this ILogger logger, Exception ex, string path);

        [LoggerMessage(Level = LogLevel.Error, Message = "Could not enumerate entries under '{Path}'")]
        public static partial void CouldNotEnumerateEntries(this ILogger logger, Exception ex, string path);

        [LoggerMessage(Level = LogLevel.Error, Message = "Save failed.")]
        public static partial void SaveFailed(this ILogger logger, Exception ex);

        [LoggerMessage(Level = LogLevel.Error, Message = "Export '{Path}' failed")]
        public static partial void ExportFailed(this ILogger logger, Exception ex, string path);

        [LoggerMessage(Level = LogLevel.Error, Message = "Failed to replace '{Path}'")]
        public static partial void ReplaceFailed(this ILogger logger, Exception ex, string path);
    }
}

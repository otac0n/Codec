// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec
{
    using System;
    using Microsoft.Extensions.Logging;

    public static partial class LoggingMessages
    {
        [LoggerMessage(Level = LogLevel.Error, Message = "Could not open nested filesystem '{Path}'")]
        public static partial void CouldNotOpenFileSystem(this ILogger logger, Exception ex, string path);
    }
}

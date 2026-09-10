// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS.Files
{
    using System;
    using System.IO;

    internal class DataCnfFile
    {
        internal static void WalkFile(Stream source, Action<string> handleSection, Action<string> handleFile)
        {
            using var reader = new StreamReader(source);
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                switch (line[0])
                {
                    case '.':
                        handleSection?.Invoke(line[1..]);
                        break;

                    case '?':
                        throw new NotImplementedException();

                    case '@':
                    default:
                        handleFile?.Invoke(line);
                        break;
                }
            }
        }
    }
}

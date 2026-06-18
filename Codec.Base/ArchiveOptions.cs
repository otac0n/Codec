// Copyright © John Gietzen. All Rights Reserved. This source is subject to the GPL license. Please see license.md for more information.

namespace Codec
{
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using Microsoft.Extensions.DependencyInjection;

    public class ArchiveOptions
    {
        public static readonly Option<string> KeyOption = new(
            name: "--key",
            description: "The key to the MGS1 alldata.bin file.",
            getDefaultValue: () => "25G/xpvTbsb+6")
        {
            IsRequired = true,
        };

        public required string Key { get; set; }

        public static void Attach(Command command)
        {
            command.AddGlobalOption(KeyOption);
        }

        public static void Bind(InvocationContext context, IServiceCollection services)
        {
            var options = new ArchiveOptions
            {
                Key = context.ParseResult.GetValueForOption(KeyOption)!,
            };

            services.AddSingleton(options);
        }
    }
}

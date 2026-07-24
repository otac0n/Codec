// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.Rendering
{
    using Codec.Rendering.Input;
    using DevDecoder.HIDDevices;
    using Microsoft.Extensions.DependencyInjection;

    public class ServiceRegistration
    {
        public static void Register(IServiceCollection services)
        {
            services.AddSingleton<Devices>();
            services.AddTransient<ControlChangeTracker>();
        }
    }
}

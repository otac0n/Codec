// Copyright © John Gietzen. All Rights Reserved. This source is subject to the GPL license. Please see license.md for more information.

namespace Codec.UI.WinForms
{
    using Microsoft.Extensions.DependencyInjection;

    internal class ServiceRegistration
    {
        internal static void Register(IServiceCollection services)
        {
            Codec.ServiceRegistration.Register(services);
            M2.ServiceRegistration.Register(services);
            MGS.ServiceRegistration.Register(services);
            services.AddTransient<Browser>();
        }
    }
}

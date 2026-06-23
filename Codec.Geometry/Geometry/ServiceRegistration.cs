// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.Geometry
{
    using Codec.Files;
    using Microsoft.Extensions.DependencyInjection;

    public static class ServiceRegistration
    {
        public static void Register(IServiceCollection services)
        {
            AssimpNetGeometryResolver.Register(services);
        }
    }
}

// Copyright © John Gietzen. All Rights Reserved. This source is subject to the GPL license. Please see license.md for more information.

namespace Codec.M2
{
    using Microsoft.Extensions.DependencyInjection;

    public static class ServiceRegistration
    {
        public static void Register(IServiceCollection services)
        {
            PsbVirtualFileSystem.Register(services);
            MVirtualFileSystem.Register(services);
            MArchiveV1VirtualFileSystem.Register(services);
        }
    }
}

// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using Filter = System.Func<System.IServiceProvider, string, string, System.IO.Abstractions.IFileSystem, string, bool>;

    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddKeyedSingleton<TService, TKey>(
            this IServiceCollection services,
            TKey serviceKey,
            Func<IServiceProvider, TKey, TService> implementationFactory)
            where TService : class =>
            services.AddKeyedSingleton<TService>(serviceKey, (s, key) => implementationFactory(s, (TKey)key!));

        public static IServiceCollection AddKeyedTransient<TService, TKey>(
            this IServiceCollection services,
            TKey serviceKey,
            Func<IServiceProvider, TKey, TService> implementationFactory)
            where TService : class =>
            services.AddKeyedTransient<TService>(serviceKey, (s, key) => implementationFactory(s, (TKey)key!));

        public static IServiceCollection AddKeyedTransient<TService, TKey>(
            this IServiceCollection services,
            Func<IServiceProvider, TKey, TService> implementationFactory)
            where TService : class =>
            services.AddKeyedTransient<TService>(KeyedService.AnyKey, (s, key) => implementationFactory(s, (TKey)key!));

        public static IServiceCollection AddFileSystem(this IServiceCollection services, string pattern, FileSystemFactory factory) =>
            AddFileSystem(services, pattern, null, factory);

        public static IServiceCollection AddFileSystem(this IServiceCollection services, string pattern, Filter? filter, FileSystemFactory factory) =>
            AddFileSystems(services, (pattern, filter, factory));

        public static IServiceCollection AddFileSystems(this IServiceCollection services, params (string Pattern, FileSystemFactory Factory)[] fileSystems) =>
            AddFileSystems(services, Array.ConvertAll(fileSystems, fs => (fs.Pattern, (Filter?)null, fs.Factory)));

        public static IServiceCollection AddFileSystems(this IServiceCollection services, params (string Pattern, Filter? Filter, FileSystemFactory Factory)[] fileSystems)
        {
            FileSystemResolver MakeResolver(string pattern, Filter? filter, FileSystemFactory factory)
            {
                var glob = PathExtensions.GlobToRegex(pattern);
                if (filter == null)
                {
                    return (servicProvider, fullPath, parentRelativePath, parent, parentPath) =>
                        glob.IsMatch(parent.Path.GetFileName(parentRelativePath)) && parent.File.Exists(parentRelativePath) ? factory : null;
                }
                else
                {
                    return (servicProvider, fullPath, parentRelativePath, parent, parentPath) =>
                        glob.IsMatch(parent.Path.GetFileName(parentRelativePath)) && parent.File.Exists(parentRelativePath) && filter(servicProvider, fullPath, parentRelativePath, parent, parentPath) ? factory : null;
                }
            }

            foreach (var (pattern, filter, factory) in fileSystems)
            {
                services.AddSingleton(MakeResolver(pattern, filter, factory));
            }

            return services;
        }
    }
}

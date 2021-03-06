﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;

namespace KubeClient
{
    /// <summary>
    ///     Extension methods for registering <see cref="KubeApiClient"/> as a component.
    /// </summary>
    public static class ClientRegistrationExtensions
    {
        /// <summary>
        ///     Add a <see cref="KubeApiClient"/> to the service collection.
        /// </summary>
        /// <param name="services">
        ///     The service collection to configure.
        /// </param>
        /// <param name="usePodServiceAccount">
        ///     Configure the client to use the service account for the current Pod?
        /// </param>
        public static void AddKubeClient(this IServiceCollection services, bool usePodServiceAccount = false)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            
            if (usePodServiceAccount)
            {
                // When running inside Kubernetes, use pod-level service account (e.g. access token from mounted Secret).
                services.AddScoped<KubeApiClient>(serviceProvider =>
                {
                    return KubeApiClient.CreateFromPodServiceAccount(
                        loggerFactory: serviceProvider.GetService<ILoggerFactory>()
                    );
                });
            }
            else
            {
                services.AddScoped<KubeApiClient>(serviceProvider =>
                {
                    KubeClientOptions options = serviceProvider.GetRequiredService<IOptions<KubeClientOptions>>().Value;

                    return KubeApiClient.Create(options,
                        loggerFactory: serviceProvider.GetService<ILoggerFactory>()
                    );
                });
            }
        }

        /// <summary>
        ///     Add a <see cref="KubeApiClient"/> to the service collection.
        /// </summary>
        /// <param name="services">
        ///     The service collection to configure.
        /// </param>
        /// <param name="options">
        ///     <see cref="KubeClientOptions"/> containing the client configuration to use.
        /// </param>
        public static void AddKubeClient(this IServiceCollection services, KubeClientOptions options)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            options.EnsureValid();

            services.AddScoped<KubeApiClient>(serviceProvider =>
            {
                return KubeApiClient.Create(options,
                    loggerFactory: serviceProvider.GetService<ILoggerFactory>()
                );
            });
        }

        /// <summary>
        ///     Add a named <see cref="KubeApiClient"/> to the service collection.
        /// </summary>
        /// <param name="services">
        ///     The service collection to configure.
        /// </param>
        /// <param name="name">
        ///     A name used to resolve the Kubernetes client.
        /// </param>
        /// <param name="configure">
        ///     A delegate that performs required configuration of the <see cref="KubeClientOptions"/> to use.
        /// </param>
        public static void AddKubeClient(this IServiceCollection services, string name, Action<KubeClientOptions> configure)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (String.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'name'.", nameof(name));

            if (configure == null)
                throw new ArgumentNullException(nameof(configure));
            
            services.AddKubeClientOptions(name, configure);
            services.AddNamedKubeClients();
        }

        /// <summary>
        ///     Add named <see cref="KubeClientOptions"/> to the service collection.
        /// </summary>
        /// <param name="services">
        ///     The service collection to configure.
        /// </param>
        /// <param name="name">
        ///     A name used to resolve the options.
        /// </param>
        /// <param name="configure">
        ///     A delegate that performs required configuration of the <see cref="KubeClientOptions"/>.
        /// </param>
        public static void AddKubeClientOptions(this IServiceCollection services, string name, Action<KubeClientOptions> configure)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (String.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'name'.", nameof(name));

            if (configure == null)
                throw new ArgumentNullException(nameof(configure));
            
            services.Configure<KubeClientOptions>(name, options =>
            {
                configure(options);
                options.EnsureValid();
            });
        }

        /// <summary>
        ///     Add support for named Kubernetes client instances.
        /// </summary>
        /// <param name="services">
        ///     The service collection to configure.
        /// </param>
        /// <returns>
        ///     The configured service collection.
        /// </returns>
        public static IServiceCollection AddNamedKubeClients(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            
            if (!services.Any(service => service.ServiceType == typeof(NamedKubeClients)))
                services.AddScoped<INamedKubeClients, NamedKubeClients>();

            return services;
        }
    }
}

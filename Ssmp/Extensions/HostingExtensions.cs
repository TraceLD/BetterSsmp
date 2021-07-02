using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ssmp.Extensions
{
    /// <summary>
    /// Extensions that make it easier to use Ssmp with ASP.NET Core Host/.NET Generic Host (Microsoft.Extensions.Hosting).
    /// </summary>
    public static class HostingExtensions
    {
        /// <summary>
        /// Adds Ssmp to the IoC container (<see cref="IServiceCollection"/>).
        ///
        /// Configures Ssmp, registers the specified <see cref="ISsmpHandler"/> implementation,
        /// registers the <see cref="ICentralServerService"/> and adds the Ssmp hosted background service - <see cref="CentralServerBackgroundService"/>. 
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="ssmpConfig">The Ssmp configuration section. Must match the <see cref="SsmpOptions"/> model.</param>
        /// <typeparam name="TSsmpHandler">The Ssmp message handler. Must implement <see cref="ISsmpHandler"/>.</typeparam>
        /// <returns></returns>
        public static IServiceCollection AddSsmp<TSsmpHandler>(
            this IServiceCollection services,
            IConfiguration ssmpConfig
        ) where TSsmpHandler : class, ISsmpHandler
        {
            services.Configure<SsmpOptions>(ssmpConfig);

            services.AddSingleton<ISsmpHandler, TSsmpHandler>();
            services.AddSingleton<ICentralServerService, CentralServerService>();

            services.AddHostedService<CentralServerBackgroundService>();

            return services;
        }
    }
}
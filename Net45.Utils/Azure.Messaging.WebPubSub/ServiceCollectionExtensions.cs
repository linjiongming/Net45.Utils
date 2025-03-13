using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Messaging.WebPubSub
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddWebPubSubService(this IServiceCollection services, string getAccessUrlApi)
        {
            services.AddSingleton(provider => new WebPubSubServiceFactory(provider.GetService<ILoggerFactory>(), getAccessUrlApi));
            return services;
        }
    }
}

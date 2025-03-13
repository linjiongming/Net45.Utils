using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace System.Collections.Concurrent
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddParallelCollection<T>(this IServiceCollection services, int maxDegreeOfParallelism)
        {
            services.AddSingleton(provider =>
                new ParallelCollection<T>(
                    provider.GetService<ILoggerFactory>(),
                    new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism }));
            return services;
        }
    }
}

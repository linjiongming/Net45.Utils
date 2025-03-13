using System;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Linq;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionDatabaseExtensions
    {
        public static IServiceCollection AddDatabase(this IServiceCollection services, string name)
        {
            var settings = ConfigurationManager.ConnectionStrings[name];
            var factory = DbProviderFactories.GetFactory(settings.ProviderName);
            services.AddTransient<IDbConnection>(provider =>
            {
                var connection = factory.CreateConnection();
                connection.ConnectionString = settings.ConnectionString;
                return connection;
            });
            var iType = typeof(IRepository);
            var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly => assembly.ExportedTypes.Where(type => type.IsClass && !type.IsAbstract && iType.IsAssignableFrom(type)));
            foreach (var type in types)
            {
                services.Add(new ServiceDescriptor(type, type, ServiceLifetime.Scoped));
            }
            return services;
        }
    }
}

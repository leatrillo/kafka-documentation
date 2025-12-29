using Coelsa.Artifact.Kafka.Handler.Interfaces;
using Coelsa.Artifact.Outbox.SqlServer.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Coelsa.Artifact.Outbox.SqlServer;
public static class OutboxSqlServerExtensions
{
    public static IServiceCollection AddCoelsaOutboxSqlServer(this IServiceCollection services)
    {
        services.AddTransient<IOutboxSqlServerCommandRepository, OutboxSqlServerCommandRepository>();

        return services;
    }
}

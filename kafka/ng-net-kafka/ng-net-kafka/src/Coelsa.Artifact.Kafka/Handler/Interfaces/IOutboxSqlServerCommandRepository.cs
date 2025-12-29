using Coelsa.Artifact.Kafka.Model.SqlServer;
using System.Data;

namespace Coelsa.Artifact.Kafka.Handler.Interfaces;
internal interface IOutboxSqlServerCommandRepository
{
    Task<bool> SaveMessageAsync(OutboxMessages message, IDbConnection connection, IDbTransaction? transaction = null);
}

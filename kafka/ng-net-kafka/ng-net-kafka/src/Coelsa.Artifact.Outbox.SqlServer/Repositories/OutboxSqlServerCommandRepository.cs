using Coelsa.Artifact.Kafka.Handler.Interfaces;
using Coelsa.Artifact.Kafka.Model.SqlServer;
using Coelsa.Artifact.Kafka.Support;
using Coelsa.Artifact.Kafka.Support.Settings;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Data;

namespace Coelsa.Artifact.Outbox.SqlServer.Repositories;
internal class OutboxSqlServerCommandRepository(IOptions<KafkaOptions> options) : IOutboxSqlServerCommandRepository
{
    private readonly SqlServerOptions options = options.Value.SqlServer;
    public async Task<bool> SaveMessageAsync(OutboxMessages message, IDbConnection connection, IDbTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        const string sql = @"INSERT INTO [{0}_OUTBOX_MESSAGES] ([OUT_ID], [OUT_TOPIC], [OUT_PAYLOAD], [OUT_SPEC_VERSION], [OUT_SOURCE], [OUT_TYPE], [OUT_TIME], [OUT_DATA_CONTENT_TYPE], [OUT_TRACE_ID], [OUT_PROCESSED_AT], [OUT_RETRY_COUNT], [OUT_NEX_RETRY_AT], [OUT_ERROR], [OUT_STATUS]) VALUES (@Id, @Topic, @Payload, @SpecVersion, @Source, @Type, @Time, @DataContentType, @TraceId, @ProcessedAt, @RetryCount, @NextRetryAt, @Error, @Status)";

        string query = string.Format(sql, options.Initials);

        DynamicParameters parameters = AddMessageParameters(message);

        TimeSpan baseDelay = TimeSpan.FromSeconds(options.RetryIntervalSeconds);

        for (int tries = 1; tries <= options.Retries; tries++)
        {
            try
            {
                if (tries > 1)
                {
                    // Backoff exponencial con jitter
                    TimeSpan delay = CoelsaKafkaTools.CalculateRetryDelay(tries, baseDelay);

                    await Task.Delay(delay);
                }

                int rowsAffected = await connection.ExecuteAsync(query, param: parameters, transaction: transaction, commandType: CommandType.Text);

                return rowsAffected == 1;
            }
            catch (SqlException sqlExc)
            {
                // Verificar si es un error transitorio y aún quedan reintentos
                if (CoelsaKafkaTools.IsTransientError(sqlExc) && tries < options.Retries)
                {
                    continue;
                }

                throw new InvalidOperationException(sqlExc.Message, sqlExc);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(ex.Message, ex);
            }
        }

        return false;
    }

    private static DynamicParameters AddMessageParameters(OutboxMessages message)
    {
        DynamicParameters parameters = new();

        parameters.Add("@Id", message.Id, DbType.String);
        parameters.Add("@Topic", message.Topic, DbType.String);
        parameters.Add("@Payload", message.Payload, DbType.String);
        parameters.Add("@SpecVersion", message.SpecVersion, DbType.String);
        parameters.Add("@Source", message.Source, DbType.String);
        parameters.Add("@Type", message.Type, DbType.String);
        parameters.Add("@Time", message.Time, DbType.DateTimeOffset);
        parameters.Add("@DataContentType", message.DataContentType, DbType.String);
        parameters.Add("@TraceId", message.TraceId, DbType.String);
        parameters.Add("@ProcessedAt", message.ProcessedAt, DbType.DateTimeOffset);
        parameters.Add("@RetryCount", message.RetryCount, DbType.Int32);
        parameters.Add("@NextRetryAt", message.NextRetryAt, DbType.DateTimeOffset);
        parameters.Add("@Error", message.Error, DbType.String);
        parameters.Add("@Status", message.Status, DbType.Int32);

        return parameters;
    }

}

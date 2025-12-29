using Coelsa.Artifact.Kafka.Model.Enum;

namespace Coelsa.Artifact.Kafka.Model;
public sealed record CoelsaMessage<T>(T Data, string Source, string Type, string SpecVersion = "1.0", CoelsaDataContentType DataContentType = CoelsaDataContentType.Json) where T : class
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public DateTimeOffset Time { get; init; } = DateTimeOffset.Now;

}





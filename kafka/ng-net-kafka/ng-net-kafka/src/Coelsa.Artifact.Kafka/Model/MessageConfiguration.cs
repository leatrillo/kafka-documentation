using Coelsa.Artifact.Kafka.Model.Enum;

namespace Coelsa.Artifact.Kafka.Model;


public sealed record MessageConfiguration(string Topic, ProducerType ProducerType = ProducerType.queue_producer);




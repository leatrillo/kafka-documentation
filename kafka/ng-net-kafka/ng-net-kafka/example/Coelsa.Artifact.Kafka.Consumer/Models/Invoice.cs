

namespace Coelsa.Artifact.Kafka.Consumer.Models
{
    internal record Invoice
    {
        public required string Id { get; init; }
        public long CustomerId { get; init; }
        public double TotalAmount { get; init; }
        public string? Currency { get; init; }
    }
}

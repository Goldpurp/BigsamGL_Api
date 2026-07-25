using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class IntegrationMessage : BaseEntity
{
    public IntegrationKind Kind { get; set; }
    public string Provider { get; set; } = "Unconfigured";
    public string Operation { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string RequestJson { get; set; } = "{}";
    public string ResponseJson { get; set; } = "{}";
    public IntegrationMessageStatus Status { get; set; }
    public DateTime ProcessedAt { get; set; }
}

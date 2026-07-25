using Domain.Common;

namespace Domain.Entities;

public sealed class CustomerLoyaltyAccount : BaseEntity
{
    public Guid CustomerId { get; set; }
    public int AvailablePoints { get; set; }
    public int LifetimeEarnedPoints { get; set; }
}

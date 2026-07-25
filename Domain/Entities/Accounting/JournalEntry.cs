using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class JournalEntry : BaseEntity
{
    public Guid? BranchId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string JournalNumber { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public Guid? SourceId { get; set; }
    public JournalStatus Status { get; set; } = JournalStatus.Draft;
    public DateTime? PostedAt { get; set; }
    public Guid? PostedByUserId { get; set; }
    public Guid? ReversalOfJournalId { get; set; }
}

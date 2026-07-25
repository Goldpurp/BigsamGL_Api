using Domain.Enums;

namespace Application.DTOs;

public record DashboardOverviewDto(
    DateTime From,
    DateTime To,
    string Currency,
    int TotalCustomers,
    int ActiveStaff,
    int QuotationsCreated,
    int AcceptedQuotations,
    int ActiveShipments,
    int DeliveredShipments,
    decimal InvoicedAmount,
    decimal CollectedAmount,
    decimal OutstandingAmount,
    decimal ApprovedExpenses,
    decimal OperatingSurplus,
    int PendingExpenseApprovals,
    IReadOnlyDictionary<string, int> SalesDocuments,
    IReadOnlyList<DashboardTrendPointDto> RevenueTrend,
    IReadOnlyList<DashboardActivityDto> RecentActivity);

public record DashboardActivityDto(
    DateTime OccurredAt,
    string Type,
    string Reference,
    string Description,
    decimal? Amount,
    string? Currency);

public record DashboardTrendPointDto(DateTime Date, decimal Amount);

public record StaffDto(
    Guid UserId, string FullName, string Email, UserRole Role, AccountStatus AccountStatus,
    string EmployeeNumber, string JobTitle, string Department, string BranchCode,
    DateTime HireDate, StaffStatus StaffStatus, Guid StaffProfileId,
    string? StatusReason = null, DateTime? ArchivedAt = null);

public record CustomerPortalDto(
    CustomerDto Customer,
    int ActiveShipments,
    int DeliveredShipments,
    IReadOnlyList<CustomerShipmentDto> RecentShipments);

public record CustomerShipmentDto(
    Guid Id,
    string TrackingNumber,
    string MarkerCode,
    string Description,
    CargoStatus Status,
    decimal BillableWeightKg,
    decimal OutstandingShippingCost,
    string Currency,
    DateTime CreatedAt)
{
    public string? InvoiceNumber { get; init; }
    public string? Method { get; init; }
    public string? Origin { get; init; }
    public string? Destination { get; init; }
    public string? HandoverMethod { get; init; }
    public int PieceCount { get; init; }
}

namespace Domain.Enums;

public enum UserRole
{
    SuperAdmin = 0,
    Administrator = 1,
    OperationsManager = 2,
    SalesManager = 3,
    FinanceManager = 4,
    Staff = 5,
    Customer = 6
}

public enum AccountStatus { Invited = 0, Active = 1, Suspended = 2, Disabled = 3 }

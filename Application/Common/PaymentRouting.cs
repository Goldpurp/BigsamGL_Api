using Domain.Enums;

namespace Application.Common;

public static class PaymentRouting
{
    public const string CashAccountCode = "1000";
    public const string BankAndCardAccountCode = "1010";

    public static bool IsSupported(PaymentMethod method) =>
        method is PaymentMethod.Cash or PaymentMethod.BankTransfer or PaymentMethod.Card;

    public static string AccountCode(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => CashAccountCode,
        PaymentMethod.BankTransfer or PaymentMethod.Card => BankAndCardAccountCode,
        _ => throw new ArgumentException("Payment method must be Cash, BankTransfer, or Card.")
    };
}


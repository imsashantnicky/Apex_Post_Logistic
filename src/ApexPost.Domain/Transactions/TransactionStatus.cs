namespace ApexPost.Domain.Transactions;

public enum TransactionStatus
{
    Open = 1,
    Paid = 2,
    Voided = 3,
    Refunded = 4,
}

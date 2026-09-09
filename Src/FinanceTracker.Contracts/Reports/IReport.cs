namespace FinanceTracker.Contracts.Reports;

public interface IReport
{
    int TotalTransactions { get; }
    decimal TotalIn { get; }
    decimal TotalOut { get; }
}

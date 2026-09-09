namespace FinanceTracker.Contracts.Reports;

public interface IReportResponse
{
    int TotalTransactions { get; }
    decimal TotalIn { get; }
    decimal TotalOut { get; }
}

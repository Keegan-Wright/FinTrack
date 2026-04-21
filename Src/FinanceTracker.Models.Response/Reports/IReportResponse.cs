namespace FinanceTracker.Models.Response.Reports;

public interface IReportResponse
{
    int TotalTransactions { get; }
    decimal TotalIn { get; }
    decimal TotalOut { get; }
}

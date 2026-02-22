using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace FinanceTracker.Models.Response.Reports
{
    public class SharedReportResponse
    {
        [Description("Total number of transactions")]
        public int TotalTransactions { get; set; }

        [Description("Total amount incoming")]
        public decimal TotalIn { get; set; }

        [Description("Total amount outgoing")]
        public decimal TotalOut { get; set; }
    }
}

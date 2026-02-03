namespace FinanceTracker.Extensions;

public static class DateTimeExtensions
{
    extension(DateTime dateTime)
    {
        public DateTime StartOfWeek(DayOfWeek startOfWeek)
        {
            int diff = (7 + (dateTime.DayOfWeek - startOfWeek)) % 7;
            return dateTime.AddDays(-1 * diff).Date;
        }
        
        public DateTime StartOfMonth()
        {
            return new DateTime(dateTime.Year, dateTime.Month, 1);
        }
        
        public DateTime StartOfYear()
        {
            return new DateTime(dateTime.Year, 1, 1);
        }
    }
}
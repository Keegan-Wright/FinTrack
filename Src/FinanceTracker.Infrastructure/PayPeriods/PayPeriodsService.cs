using System.Runtime.CompilerServices;
using FinanceTracker.Contracts;
using FinanceTracker.Contracts.PayPeriods;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.PayPeriods;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PayPeriod = FinanceTracker.Contracts.PayPeriods.PayPeriod;

namespace FinanceTracker.Infrastructure.PayPeriods;

/// <inheritdoc cref="IPayPeriodsService"/>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IPayPeriodsService>]
public class PayPeriodsService : ServiceBase<PayPeriodsService>, IPayPeriodsService
{
    private readonly TimeProvider _timeProvider;

    /// <param name="currentUser">The signed in user, or who the work runs as.</param>
    /// <param name="financeTrackerContextFactory">Makes the contexts it reads and saves through.</param>
    /// <param name="logger">Its logger.</param>
    /// <param name="timeProvider">The clock.</param>
    public PayPeriodsService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<PayPeriodsService> logger,
        TimeProvider timeProvider) : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public async Task<YourPay> GetYourPayAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        PaySchedule? schedule = await context.PaySchedules.OwnedBy(UserId).AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        return await ToYourPayAsync(context, schedule, cancellationToken);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<Income> GetIncomesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        IReadOnlyList<DetectedPay> detected =
            await PayTransactions.DetectAsync(context, UserId, _timeProvider, cancellationToken);

        Dictionary<Guid, string> accountNames = await AccountNamesAsync(context, cancellationToken);

        foreach (DetectedPay pay in detected)
        {
            yield return ToIncome(pay.AccountId, pay.Key, pay.Name, pay.Amount, pay.Pattern, pay.PayDays[^1],
                accountNames);
        }
    }

    /// <inheritdoc/>
    public async Task<YourPay> ChooseAsync(ChooseIncome income, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(income);

        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        IReadOnlyList<DetectedPay> detected =
            await PayTransactions.DetectAsync(context, UserId, _timeProvider, cancellationToken);

        DetectedPay chosen = detected.FirstOrDefault(x => x.AccountId == income.AccountId && x.Key == income.Key)
                             ?? throw new DomainRuleException("Nothing coming in like pay matches that income");

        PaySchedule? schedule = await context.PaySchedules.OwnedBy(UserId).SingleOrDefaultAsync(cancellationToken);
        if (schedule is null)
        {
            schedule = new PaySchedule(UserId, chosen);
            context.PaySchedules.Add(schedule);
        }

        schedule.Choose(chosen);
        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChangedTheir(UserId, "chose", "pay");
        return await ToYourPayAsync(context, schedule, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<YourPay> TurnOffAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        PaySchedule? schedule = await context.PaySchedules.OwnedBy(UserId).SingleOrDefaultAsync(cancellationToken);
        if (schedule is not null)
        {
            schedule.TurnOff();
            await context.SaveChangesAsync(cancellationToken);
            Logger.LogChangedTheir(UserId, "turned off", "pay periods");
        }

        return await ToYourPayAsync(context, schedule, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<YourPay> TurnOnAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        PaySchedule schedule = await context.PaySchedules.OwnedBy(UserId).SingleOrDefaultAsync(cancellationToken)
                               ?? throw new NotFoundException("No pay has been found yet");

        schedule.TurnOn();
        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChangedTheir(UserId, "turned on", "pay periods");
        return await ToYourPayAsync(context, schedule, cancellationToken);
    }

    /// <summary>
    /// The user's pay and periods as the API sends them.
    /// </summary>
    /// <param name="context">The context to read through.</param>
    /// <param name="schedule">The user's pay, null when it isn't known.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task<YourPay> ToYourPayAsync(FinanceTrackerContext context, PaySchedule? schedule,
        CancellationToken cancellationToken)
    {
        DateOnly today = _timeProvider.Today;
        PayCalendar calendar = schedule?.Calendar(today) ?? PayCalendar.CalendarMonths;
        PayPeriod current = ToContract(calendar.Containing(today));

        if (schedule is null)
        {
            return new YourPay { CurrentPeriod = current };
        }

        Dictionary<Guid, string> accountNames = await AccountNamesAsync(context, cancellationToken);
        return new YourPay
        {
            Pay = ToIncome(schedule.AccountId, schedule.Key, schedule.Name, schedule.Amount, schedule.Pattern,
                schedule.PayDays.Max(), accountNames),
            IsChosen = schedule.IsChosen,
            IsOff = schedule.IsOff,
            FollowsPay = calendar.FollowsPay,
            CurrentPeriod = current
        };
    }

    /// <summary>
    /// The user's accounts' names, by id.
    /// </summary>
    /// <param name="context">The context to read through.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private Task<Dictionary<Guid, string>> AccountNamesAsync(FinanceTrackerContext context,
        CancellationToken cancellationToken)
    {
        return context.BankAccounts.OwnedBy(UserId).AsNoTracking()
            .ToDictionaryAsync(static x => x.Id, static x => x.DisplayName, cancellationToken);
    }

    /// <summary>
    /// An income as the API sends it.
    /// </summary>
    /// <param name="accountId">The account it's paid into.</param>
    /// <param name="key">The payer in upper case letters only.</param>
    /// <param name="name">The payer as the bank names them.</param>
    /// <param name="amount">The latest pay.</param>
    /// <param name="pattern">When it comes each month.</param>
    /// <param name="lastPaidOn">When it last came.</param>
    /// <param name="accountNames">The user's accounts' names, by id.</param>
    private static Income ToIncome(Guid accountId, string key, string name, decimal amount, PayPattern pattern,
        DateOnly lastPaidOn, Dictionary<Guid, string> accountNames) => new()
    {
        AccountId = accountId,
        AccountName = accountNames.GetValueOrDefault(accountId, string.Empty),
        Key = key,
        Name = name,
        Amount = amount,
        // The contract keeps the domain's values
        Kind = (PayDayKind)pattern.Rule,
        Day = pattern.Day,
        LastPaidOn = lastPaidOn
    };

    /// <summary>
    /// A period as the API sends it.
    /// </summary>
    /// <param name="period">The period.</param>
    internal static PayPeriod ToContract(Domain.PayPeriods.PayPeriod period) => new()
    {
        Start = period.Start,
        End = period.End,
        Month = new DateOnly(period.Year, period.Month, 1)
    };
}

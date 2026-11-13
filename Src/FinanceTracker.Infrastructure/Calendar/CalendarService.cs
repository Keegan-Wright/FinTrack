using System.Runtime.CompilerServices;
using FinanceTracker.Contracts;
using FinanceTracker.Contracts.Calendar;
using FinanceTracker.Contracts.Debts;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Bills;
using FinanceTracker.Infrastructure.Sharing;
using FinanceTracker.Infrastructure.Persistence;
using FinanceTracker.Infrastructure.SavingsGoals;
using FinanceTracker.Domain.Sharing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CalendarEvent = FinanceTracker.Contracts.Calendar.CalendarEvent;
using ContractSavingsGoal = FinanceTracker.Contracts.SavingsGoals.SavingsGoal;
using DomainCalendarEvent = FinanceTracker.Domain.Calendar.CalendarEvent;
using DomainSavingsGoal = FinanceTracker.Domain.SavingsGoals.SavingsGoal;

namespace FinanceTracker.Infrastructure.Calendar;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<ICalendarService>]
public class CalendarService : ServiceBase<CalendarService>, ICalendarService
{
    private readonly TimeProvider _timeProvider;
    private readonly ISavingsGoalsService _savingsGoalsService;
    private readonly IDebtsService _debtsService;
    private readonly IAccountScope _accountScope;

    public CalendarService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<CalendarService> logger,
        TimeProvider timeProvider, ISavingsGoalsService savingsGoalsService, IDebtsService debtsService,
        IAccountScope accountScope)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _accountScope = accountScope;
        _timeProvider = timeProvider;
        _savingsGoalsService = savingsGoalsService;
        _debtsService = debtsService;
    }

    public async IAsyncEnumerable<CalendarItems> GetMonthItemsAsync(int month, int year,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        int daysInMonth = DateTime.DaysInMonth(year, month);
        DateTime startDate = new(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime endDate = startDate.AddMonths(1);
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Transaction timestamps / goal dates are encrypted at rest, so the month's transactions are found by their day
        // blind index, then filtered exactly once decrypted.
        // The calendar is income and spending, so transfers are left out like the dashboard's totals
        AccountsInView view = await _accountScope.ResolveAsync(context, cancellationToken);
        List<CalendarTransactionItem> transactionItems = (await context.BankTransactions.InView(view)
                .WhereEncryptedDayBetween(x => x.OccurredAt, startDate, endDate, context)
                .Where(static x => x.Transfer == null || x.Transfer.Decision != TransferDecision.Transfer)
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .Select(x => new CalendarTransactionItem
            {
                Description = view.DescriptionOf(x),
                Amount = x.Amount,
                TransactionType = x.Type.ToString(),
                TransactionTime = x.OccurredAt
            })
            .ToList();

        List<IGrouping<DateTime, CalendarTransactionItem>> transactions = transactionItems
            .Where(x => x.TransactionTime >= startDate && x.TransactionTime < endDate)
            .GroupBy(static x => x.TransactionTime.Date)
            .ToList();

        // Their own goals and those shared with them, outside Just mine
        List<CalendarGoalItem> goalItems = (await SharedGoals.ForAsync(context, UserId, view, cancellationToken))
            .Select(static x => new CalendarGoalItem { Name = x.Item.Name, GoalCompletionDate = x.Item.TargetDate })
            .ToList();

        List<IGrouping<DateTime, CalendarGoalItem>> goals = goalItems
            .Where(x => x.GoalCompletionDate is { } goalDate && goalDate >= startDate && goalDate < endDate)
            .GroupBy(static x => x.GoalCompletionDate!.Value.Date)
            .ToList();

        DateOnly first = DateOnly.FromDateTime(startDate);
        DateOnly last = DateOnly.FromDateTime(endDate).AddDays(-1);

        Dictionary<DateOnly, List<CalendarBillItem>> bills = await LoadBillsAsync(context, view, first, last,
            cancellationToken);

        Dictionary<DateOnly, List<CalendarEventItem>> events = await LoadEventsAsync(context, view, first, last,
            cancellationToken);

        Dictionary<DateOnly, List<CalendarDebtItem>> debts = await LoadDebtsAsync(view, first, last,
            cancellationToken);

        for (int i = 0; i < daysInMonth; i++)
        {
            DateTime date = startDate.AddDays(i);
            DateOnly day = DateOnly.FromDateTime(date);

            CalendarItems response = new()
            {
                Date = date,
                Transactions = transactions.Where(x => x.Key == date).SelectMany(static x => x),
                Goals = goals.Where(x => x.Key == date).SelectMany(static x => x),
                Bills = bills.GetValueOrDefault(day, []),
                Events = events.GetValueOrDefault(day, []),
                Debts = debts.GetValueOrDefault(day, [])
            };

            yield return response;
        }
    }

    public async IAsyncEnumerable<CalendarEvent> GetEventsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        // Their own and those shared with them
        List<VisibleItem<DomainCalendarEvent>> calendarEvents = await VisibleEventsAsync(context, justMine: false,
            cancellationToken);
        Dictionary<Guid, string> goalNames = await LoadGoalNamesAsync(context,
            calendarEvents.Select(static x => x.Item).ToList(), cancellationToken);
        DateOnly today = _timeProvider.Today;

        // Dates and titles are encrypted, so they're ordered once decrypted
        IEnumerable<CalendarEvent> ordered = calendarEvents
            .Select(x => CalendarEventMapper.ToContract(x.Item, GoalName(x.Item, goalNames), today, x.SharedBy,
                x.CanChange))
            .OrderBy(static x => x.NextOn is null)
            .ThenBy(static x => x.NextOn)
            .ThenBy(static x => x.Title, StringComparer.CurrentCultureIgnoreCase);

        foreach (CalendarEvent calendarEvent in ordered)
        {
            yield return calendarEvent;
        }
    }

    public async Task<CalendarEvent> AddEventAsync(SaveCalendarEvent calendarEvent,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        DomainCalendarEvent added = new(UserId, calendarEvent.Title, calendarEvent.Date,
            CalendarEventMapper.ToDomain(calendarEvent.Recurrence));
        DomainSavingsGoal? goal = await ApplyAsync(context, added, calendarEvent, cancellationToken);

        context.CalendarEvents.Add(added);
        await NameCheck.SaveKeepingNamesApartAsync(context, NameTakenException.SavingsGoal, calendarEvent.Title,
            cancellationToken);
        Logger.LogChanged(UserId, "created", "calendar event", added.Id);
        return CalendarEventMapper.ToContract(added, goal?.Name, _timeProvider.Today);
    }

    public async Task<CalendarEvent> UpdateEventAsync(Guid id, SaveCalendarEvent calendarEvent,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Their own, or one shared with them with changes allowed, whose savings stay its owner's to set
        VisibleItem<DomainCalendarEvent> visible = await VisibleEventAsync(context, id, mustChange: true,
            cancellationToken);
        DomainCalendarEvent updated = await context.CalendarEvents.SingleAsync(x => x.Id == id, cancellationToken);

        updated.Rename(calendarEvent.Title);
        updated.Reschedule(calendarEvent.Date, CalendarEventMapper.ToDomain(calendarEvent.Recurrence));

        // After rescheduling, which stops a one-off event moving its goal on, so that isn't counted as a change
        updated.EnsureSavingsAreUnchangedFor(UserId, calendarEvent.SavingsTarget, calendarEvent.SavingsGoalId,
            calendarEvent.CreateSavingsGoal, calendarEvent.RollsGoalOn);
        string? goalName;
        if (visible.IsOwn)
        {
            DomainSavingsGoal? goal = await ApplyAsync(context, updated, calendarEvent, cancellationToken);
            goalName = goal?.Name;
        }
        else
        {
            updated.ChangeNotes(calendarEvent.Notes);
            goalName = GoalName(updated, await LoadGoalNamesAsync(context, [updated], cancellationToken));
        }

        await NameCheck.SaveKeepingNamesApartAsync(context, NameTakenException.SavingsGoal, calendarEvent.Title,
            cancellationToken);
        Logger.LogChanged(UserId, "updated", "calendar event", id);
        return CalendarEventMapper.ToContract(updated, goalName, _timeProvider.Today, visible.SharedBy,
            visible.CanChange);
    }

    public async Task DeleteEventAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        DomainCalendarEvent calendarEvent = await FindEventAsync(context, id, cancellationToken);

        context.CalendarEvents.Remove(calendarEvent);
        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChanged(UserId, "deleted", "calendar event", id);
    }

    private async Task<DomainCalendarEvent> FindEventAsync(FinanceTrackerContext context, Guid id,
        CancellationToken cancellationToken)
    {
        DomainCalendarEvent? calendarEvent = await context.CalendarEvents.OwnedBy(UserId)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return calendarEvent ?? throw new NotFoundException($"The user has no event {id}.");
    }

    /// <summary>
    /// Everything but its title and date onto <paramref name="target"/>, creating or finding the goal it saves in.
    /// </summary>
    /// <returns>Its goal, null without one.</returns>
    private async Task<DomainSavingsGoal?> ApplyAsync(FinanceTrackerContext context, DomainCalendarEvent target,
        SaveCalendarEvent calendarEvent, CancellationToken cancellationToken)
    {
        target.ChangeNotes(calendarEvent.Notes);
        target.SetSavingsTarget(calendarEvent.SavingsTarget);

        DomainSavingsGoal? goal = await GoalForAsync(context, target, calendarEvent, cancellationToken);
        if (goal is null)
        {
            target.UnlinkSavingsGoal();
            return null;
        }

        target.LinkSavingsGoal(goal);
        target.SetRollsGoalOn(calendarEvent.RollsGoalOn);
        return goal;
    }

    /// <summary>
    /// A new goal named after the event and aimed at its target by its next date, the user's chosen goal, or none.
    /// </summary>
    /// <exception cref="InvalidOperationException">The chosen goal isn't the user's.</exception>
    /// <exception cref="NameTakenException">A new goal's name, the event's title, is already a goal's.</exception>
    private async Task<DomainSavingsGoal?> GoalForAsync(FinanceTrackerContext context, DomainCalendarEvent target,
        SaveCalendarEvent calendarEvent, CancellationToken cancellationToken)
    {
        if (calendarEvent.CreateSavingsGoal)
        {
            await SavingsGoalNames.EnsureFreeAsync(context, UserId, target.Title, null, cancellationToken);
            DateOnly targetDate = target.NextOn(_timeProvider.Today) ?? target.Date;
            DomainSavingsGoal created = new(UserId, target.Title, 0, 0, calendarEvent.SavingsTarget ?? 0,
                targetDate.ToDateTime(TimeOnly.MinValue));
            context.SavingsGoals.Add(created);
            return created;
        }

        if (calendarEvent.SavingsGoalId is not { } goalId)
        {
            return null;
        }

        DomainSavingsGoal? goal = await context.SavingsGoals.OwnedBy(UserId)
            .SingleOrDefaultAsync(x => x.Id == goalId, cancellationToken);

        return goal ?? throw new NotFoundException($"The user has no savings goal {goalId}.");
    }

    /// <summary>
    /// The user's events on each day from <paramref name="first"/> to <paramref name="last"/>, yearly ones each year,
    /// with what's saved in their goals.
    /// </summary>
    private async Task<Dictionary<DateOnly, List<CalendarEventItem>>> LoadEventsAsync(FinanceTrackerContext context,
        AccountsInView view, DateOnly first, DateOnly last, CancellationToken cancellationToken)
    {
        // Their own and, outside Just mine, those shared with them. Dates are encrypted, so each event is checked once
        // decrypted
        List<VisibleItem<DomainCalendarEvent>> calendarEvents = await VisibleEventsAsync(context, view.IsJustMine,
            cancellationToken);

        Dictionary<DateOnly, List<CalendarEventItem>> byDay = [];
        if (calendarEvents.Count == 0)
        {
            return byDay;
        }

        Dictionary<Guid, ContractSavingsGoal> goals = await LoadGoalsAsync(
            calendarEvents.Select(static x => x.Item).ToList(), cancellationToken);
        for (DateOnly day = first; day <= last; day = day.AddDays(1))
        {
            foreach ((DomainCalendarEvent calendarEvent, string? sharedBy, _) in calendarEvents
                         .Where(x => x.Item.OccursOn(day)))
            {
                ContractSavingsGoal? goal = null;
                if (calendarEvent.SavingsGoalId is { } goalId)
                {
                    goal = goals.GetValueOrDefault(goalId);
                }

                AddTo(byDay, day, CalendarEventMapper.ToItem(calendarEvent, goal?.Name, goal?.Saved, sharedBy));
            }
        }

        return byDay;
    }

    /// <summary>
    /// The user's goals with what each has saved, when any event saves in one.
    /// </summary>
    private async Task<Dictionary<Guid, ContractSavingsGoal>> LoadGoalsAsync(
        List<DomainCalendarEvent> calendarEvents, CancellationToken cancellationToken)
    {
        if (calendarEvents.All(static x => x.SavingsGoalId is null))
        {
            return [];
        }

        List<ContractSavingsGoal> goals = await _savingsGoalsService.GetSavingsGoalsAsync(cancellationToken)
            .ToListAsync(cancellationToken);
        return goals.ToDictionary(static x => x.Id);
    }

    /// <summary>
    /// The names of the goals <paramref name="calendarEvents"/> save in, whoever's, as a shared event's goal is its
    /// owner's.
    /// </summary>
    private static async Task<Dictionary<Guid, string>> LoadGoalNamesAsync(FinanceTrackerContext context,
        List<DomainCalendarEvent> calendarEvents, CancellationToken cancellationToken)
    {
        List<Guid> goalIds = calendarEvents.Select(static x => x.SavingsGoalId).OfType<Guid>().ToList();
        return await context.SavingsGoals.AsNoTracking()
            .Where(x => goalIds.Contains(x.Id))
            .ToDictionaryAsync(static x => x.Id, static x => x.Name, cancellationToken);
    }

    /// <summary>
    /// The user's events and, unless <paramref name="justMine"/>, those shared with them.
    /// </summary>
    private Task<List<VisibleItem<DomainCalendarEvent>>> VisibleEventsAsync(FinanceTrackerContext context,
        bool justMine, CancellationToken cancellationToken) =>
        SharedChosenItems.ForAsync(context, context.CalendarEvents, UserId, ShareItemType.CalendarEvent, justMine,
            cancellationToken);

    /// <summary>
    /// Event <paramref name="id"/>, the user's own or one shared with them.
    /// </summary>
    /// <param name="mustChange">Whether they must be allowed to change it.</param>
    /// <exception cref="InvalidOperationException">They can't see it, or can't change it.</exception>
    private async Task<VisibleItem<DomainCalendarEvent>> VisibleEventAsync(FinanceTrackerContext context, Guid id,
        bool mustChange, CancellationToken cancellationToken)
    {
        VisibleItem<DomainCalendarEvent>? found = (await VisibleEventsAsync(context, justMine: false,
                cancellationToken))
            .SingleOrDefault(x => x.Item.Id == id);
        if (found is null || (mustChange && !found.CanChange))
        {
            throw new NotFoundException($"No event {id} the user can see or change was found.");
        }

        return found;
    }

    private static string? GoalName(DomainCalendarEvent calendarEvent, Dictionary<Guid, string> goalNames)
    {
        if (calendarEvent.SavingsGoalId is not { } goalId)
        {
            return null;
        }

        return goalNames.GetValueOrDefault(goalId);
    }

    /// <summary>
    /// Whether <paramref name="debt"/> belongs in <paramref name="view"/>: their own, or shared outside Just mine, or
    /// following an account still in it, such as a joint account's.
    /// </summary>
    private static bool InView(Debt debt, AccountsInView view)
    {
        if (debt.SharedBy is null || !view.IsJustMine)
        {
            return true;
        }

        return debt.AccountId is { } accountId && view.Contains(accountId);
    }

    /// <summary>
    /// Each debt still owing's forecast pay-off day and its goal, from <paramref name="first"/> to
    /// <paramref name="last"/>. Their own and those shared with them, though Just mine leaves out those shared, as
    /// <paramref name="view"/> does their accounts.
    /// </summary>
    private async Task<Dictionary<DateOnly, List<CalendarDebtItem>>> LoadDebtsAsync(AccountsInView view,
        DateOnly first, DateOnly last, CancellationToken cancellationToken)
    {
        Dictionary<DateOnly, List<CalendarDebtItem>> byDay = [];

        await foreach (Debt debt in _debtsService.GetDebtsAsync(cancellationToken))
        {
            if (debt.Owed <= 0 || !InView(debt, view))
            {
                continue;
            }

            if (debt.Forecast.PaidOffOn is { } paidOffOn && paidOffOn >= first && paidOffOn <= last)
            {
                AddTo(byDay, paidOffOn,
                    new CalendarDebtItem { DebtId = debt.Id, Name = debt.Name, Kind = CalendarDebtDate.PaidOff });
            }

            if (debt.PayOffGoal is { } goal && goal >= first && goal <= last)
            {
                AddTo(byDay, goal, new CalendarDebtItem
                {
                    DebtId = debt.Id, Name = debt.Name, Kind = CalendarDebtDate.Goal, OnTrack = debt.OnTrack
                });
            }
        }

        return byDay;
    }

    private static void AddTo<T>(Dictionary<DateOnly, List<T>> byDay, DateOnly day, T item)
    {
        if (!byDay.TryGetValue(day, out List<T>? items))
        {
            items = [];
            byDay.Add(day, items);
        }

        items.Add(item);
    }

    /// <summary>
    /// Each bill's periods due from <paramref name="first"/> to <paramref name="last"/>, by the day they're due. Their
    /// own and those shared with them through <paramref name="view"/>, so Just mine leaves out the shared ones.
    /// </summary>
    private async Task<Dictionary<DateOnly, List<CalendarBillItem>>> LoadBillsAsync(FinanceTrackerContext context,
        AccountsInView view, DateOnly first, DateOnly last, CancellationToken cancellationToken)
    {
        List<Bill> bills = (await SharedBills.ForAsync(context, UserId, view, cancellationToken))
            .Select(static x => x.Item)
            .ToList();

        List<Guid> billIds = bills.Select(static x => x.Id).ToList();

        List<BankTransaction> payments = await context.BankTransactions.InView(view).AsNoTracking()
            .Where(x => x.BillPayment != null && x.BillPayment.BillId != null &&
                        billIds.Contains(x.BillPayment.BillId.Value))
            .ToListAsync(cancellationToken);

        DateOnly today = _timeProvider.Today;
        Dictionary<DateOnly, List<CalendarBillItem>> byDay = [];

        foreach (Bill bill in bills)
        {
            foreach (BillPeriod period in BillCalculator.Periods(bill, payments, first, last,
                         _timeProvider.LocalTimeZone, today))
            {
                AddTo(byDay, period.Due, ToItem(bill, period));
            }
        }

        return byDay;
    }

    private static CalendarBillItem ToItem(Bill bill, BillPeriod period)
    {
        decimal amount = period.Expected;
        if (period.PaymentIds.Count > 0)
        {
            amount = period.Paid;
        }

        return new CalendarBillItem
        {
            BillId = bill.Id,
            Name = bill.Name,
            Amount = amount,
            State = Enum.Parse<Contracts.Bills.BillState>(period.State.ToString())
        };
    }
}

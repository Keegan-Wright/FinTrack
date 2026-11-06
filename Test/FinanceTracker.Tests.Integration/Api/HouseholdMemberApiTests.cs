using System.Net;
using FinanceTracker.Contracts;
using FinanceTracker.Contracts.HouseholdMember;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Tests.Integration.Http;
using HouseholdMember = FinanceTracker.Contracts.HouseholdMember.HouseholdMember;

namespace FinanceTracker.Tests.Integration.Api;

public class HouseholdMemberApiTests : IntegrationTestBase
{
    [Test]
    public async Task AddHouseholdMember_IsListedAndCanBeDeleted()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IHouseholdMemberService service = ApiFor(user).Get<IHouseholdMemberService>();

        // Act
        HouseholdMember added = await service.AddHouseholdMemberAsync(
            new AddHouseholdMember { FirstName = "Jane", LastName = "Doe", YearlyIncome = 2500m }, CancellationToken);
        List<HouseholdMember> listed = await service.GetHouseholdMembersAsync(CancellationToken).ToListAsync(CancellationToken);
        bool deleted = await service.DeleteHouseholdMemberAsync(added.Id, CancellationToken);
        List<HouseholdMember> afterDelete = await service.GetHouseholdMembersAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(added.FirstName).IsEqualTo("Jane");
            await Assert.That(added.YearlyIncome).IsEqualTo(2500m);
            await Assert.That(listed.Select(x => x.Id)).IsEquivalentTo([added.Id]);
            await Assert.That(deleted).IsTrue();
            await Assert.That(afterDelete).IsEmpty();
        }
    }

    [Test]
    public async Task DeleteHouseholdMember_OfAnotherUser_IsNotFound()
    {
        // Arrange
        SignedInUser owner = await RegisterUserAsync();
        SignedInUser other = await RegisterUserAsync();
        HouseholdMember theirs = await ApiFor(owner).Get<IHouseholdMemberService>().AddHouseholdMemberAsync(
            new AddHouseholdMember { FirstName = "Jane", LastName = "Doe", YearlyIncome = 1m }, CancellationToken);

        // Act
        bool deleted = await ApiFor(other).Get<IHouseholdMemberService>()
            .DeleteHouseholdMemberAsync(theirs.Id, CancellationToken);

        // Assert
        await Assert.That(deleted).IsFalse();
    }

    [Test]
    public async Task AddHouseholdMember_ThatFailsValidation_IsRejected()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IHouseholdMemberService service = ApiFor(user).Get<IHouseholdMemberService>();

        // Act
        HttpRequestException? exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.AddHouseholdMemberAsync(
                new AddHouseholdMember { FirstName = "", LastName = "Doe", YearlyIncome = -1m }, CancellationToken));

        // Assert
        await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task UpdateHouseholdMember_IsSavedAndListed()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IHouseholdMemberService service = ApiFor(user).Get<IHouseholdMemberService>();
        HouseholdMember added = await service.AddHouseholdMemberAsync(
            new AddHouseholdMember { FirstName = "Jane", LastName = "Doe", YearlyIncome = 2500m }, CancellationToken);

        // Act
        HouseholdMember updated = await service.UpdateHouseholdMemberAsync(added.Id,
            new UpdateHouseholdMember { FirstName = "Janet", LastName = "Doe", YearlyIncome = 2750m }, CancellationToken);
        HouseholdMember listed = (await service.GetHouseholdMembersAsync(CancellationToken)
            .ToListAsync(CancellationToken)).Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(updated.FirstName).IsEqualTo("Janet");
            await Assert.That(listed.YearlyIncome).IsEqualTo(2750m);
        }
    }

    [Test]
    public async Task UpdateHouseholdMember_OfAnotherUser_ThrowsAsTheServiceDoes()
    {
        // Arrange
        SignedInUser owner = await RegisterUserAsync();
        SignedInUser other = await RegisterUserAsync();
        HouseholdMember theirs = await ApiFor(owner).Get<IHouseholdMemberService>().AddHouseholdMemberAsync(
            new AddHouseholdMember { FirstName = "Jane", LastName = "Doe", YearlyIncome = 1m }, CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => ApiFor(other).Get<IHouseholdMemberService>()
            .UpdateHouseholdMemberAsync(theirs.Id,
                new UpdateHouseholdMember { FirstName = "A", LastName = "B", YearlyIncome = 0m }, CancellationToken));
    }

    [Test]
    public async Task AddHouseholdMember_SameNameAsSomeoneInTheHousehold_ThrowsAsTheServiceDoes()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IHouseholdMemberService service = ApiFor(user).Get<IHouseholdMemberService>();
        await service.AddHouseholdMemberAsync(
            new AddHouseholdMember { FirstName = "Jane", LastName = "Doe", YearlyIncome = 1m }, CancellationToken);

        // Act
        NameTakenException? exception = await Assert.ThrowsAsync<NameTakenException>(() =>
            service.AddHouseholdMemberAsync(
                new AddHouseholdMember { FirstName = "Jane", LastName = "Doe", YearlyIncome = 2m }, CancellationToken));

        // Assert
        await Assert.That(exception!.Message).IsEqualTo("You already have a person called Jane Doe");
    }

    [Test]
    public async Task UpdateHouseholdMember_ThatFailsValidation_IsRejected()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IHouseholdMemberService service = ApiFor(user).Get<IHouseholdMemberService>();
        HouseholdMember added = await service.AddHouseholdMemberAsync(
            new AddHouseholdMember { FirstName = "Jane", LastName = "Doe", YearlyIncome = 1m }, CancellationToken);

        // Act
        HttpRequestException? exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.UpdateHouseholdMemberAsync(added.Id,
                new UpdateHouseholdMember { FirstName = "", LastName = "Doe", YearlyIncome = -1m }, CancellationToken));

        // Assert
        await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task UpdateRecognition_IsSavedAndListed()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IHouseholdMemberService service = ApiFor(user).Get<IHouseholdMemberService>();
        HouseholdMember added = await service.AddHouseholdMemberAsync(
            new AddHouseholdMember { FirstName = "Jane", LastName = "Doe", YearlyIncome = 1m }, CancellationToken);

        // Act
        HouseholdMemberRecognitionSaved saved = await service.UpdateRecognitionAsync(added.Id,
            new UpdateHouseholdMemberRecognition
            {
                PayeeNames = ["J Doe"],
                LinkedAccounts = [new LinkedAccount { SortCode = "11-22-33", Number = "12345678" }]
            }, CancellationToken);
        HouseholdMember listed = await service.GetHouseholdMembersAsync(CancellationToken).SingleAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(saved.RecognisedTransactions).IsEqualTo(0);
            await Assert.That(listed.PayeeNames).IsEquivalentTo(["J Doe"]);
            await Assert.That(listed.LinkedAccounts.Select(x => $"{x.SortCode} {x.Number}"))
                .IsEquivalentTo(["112233 12345678"]);
        }
    }

    [Test]
    public async Task UpdateRecognition_OfAnotherUsersMember_IsNotFound()
    {
        // Arrange
        SignedInUser owner = await RegisterUserAsync();
        SignedInUser other = await RegisterUserAsync();
        HouseholdMember theirs = await ApiFor(owner).Get<IHouseholdMemberService>().AddHouseholdMemberAsync(
            new AddHouseholdMember { FirstName = "Jane", LastName = "Doe", YearlyIncome = 1m }, CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => ApiFor(other).Get<IHouseholdMemberService>()
            .UpdateRecognitionAsync(theirs.Id, new UpdateHouseholdMemberRecognition(), CancellationToken));
    }

    [Test]
    public async Task UpdateRecognition_WithAnInvalidAccount_IsRejected()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IHouseholdMemberService service = ApiFor(user).Get<IHouseholdMemberService>();
        HouseholdMember added = await service.AddHouseholdMemberAsync(
            new AddHouseholdMember { FirstName = "Jane", LastName = "Doe", YearlyIncome = 1m }, CancellationToken);

        // Act
        HttpRequestException? exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.UpdateRecognitionAsync(added.Id, new UpdateHouseholdMemberRecognition
            {
                LinkedAccounts = [new LinkedAccount { SortCode = "1122", Number = "12345678" }]
            }, CancellationToken));

        // Assert
        await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}

using System.Net;
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
            new AddHouseholdMember { FirstName = "Jane", LastName = "Doe", Income = 2500m }, CancellationToken);
        List<HouseholdMember> listed = await service.GetHouseholdMembersAsync(CancellationToken).ToListAsync(CancellationToken);
        bool deleted = await service.DeleteHouseholdMemberAsync(added.Id, CancellationToken);
        List<HouseholdMember> afterDelete = await service.GetHouseholdMembersAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(added.FirstName).IsEqualTo("Jane");
            await Assert.That(added.Income).IsEqualTo(2500m);
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
            new AddHouseholdMember { FirstName = "Jane", LastName = "Doe", Income = 1m }, CancellationToken);

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
                new AddHouseholdMember { FirstName = "", LastName = "Doe", Income = -1m }, CancellationToken));

        // Assert
        await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}

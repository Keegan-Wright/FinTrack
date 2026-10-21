using FinanceTracker.Domain.Identity;

namespace FinanceTracker.Tests.Unit.Domain.Identity;

[Category("Domain")]
public class FinanceTrackerUserTests
{

    [Test]
    public async Task FinanceTrackerUser_Construction_SetsProperties()
    {
        var user = new FinanceTrackerUser("Jane", "Doe", "jane@test.local", "jdoe");

        using (Assert.Multiple())
        {
            await Assert.That(user.FirstName).IsEqualTo("Jane");
            await Assert.That(user.LastName).IsEqualTo("Doe");
            await Assert.That(user.Email).IsEqualTo("jane@test.local");
            await Assert.That(user.UserName).IsEqualTo("jdoe");
            await Assert.That(user.OpenIdConnectSubject).IsNull();
        }
    }

    [Test]
    public async Task FinanceTrackerUser_OidcConstruction_SetsSubject()
    {
        var user = new FinanceTrackerUser("Jane", "Doe", "jane@test.local", "jdoe", "oidc-subject");

        using (Assert.Multiple())
        {
            await Assert.That(user.UserName).IsEqualTo("jdoe");
            await Assert.That(user.OpenIdConnectSubject).IsEqualTo("oidc-subject");
        }
    }

    [Test]
    public async Task FinanceTrackerUser_SetOidcSubject_UpdatesSubject()
    {
        var user = new FinanceTrackerUser("Jane", "Doe", "jane@test.local", "jdoe", "old-subject");

        user.SetOidcSubject("new-subject");

        await Assert.That(user.OpenIdConnectSubject).IsEqualTo("new-subject");
    }
}

using System.Buffers.Binary;
using FinanceTracker.Contracts.Auth;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Extensions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Account.Authenticator;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Telemetry;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Extensions;

public class IdentitySecurityExtensionsTests : InfrastructureTestBase
{
    private readonly TestMetrics _metrics = new();
    private ServiceProvider _services = null!;
    private AsyncServiceScope _scope;

    private UserManager<FinanceTrackerUser> UserManager =>
        _scope.ServiceProvider.GetRequiredService<UserManager<FinanceTrackerUser>>();

    [Before(Test)]
    public void SetupIdentity()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddDataProtection();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(_metrics.Metrics);
        services.AddScoped(_ => DbContextFactory.CreateDbContext());
        services.AddIdentityCore<FinanceTrackerUser>()
            .AddEntityFrameworkStores<FinanceTrackerContext>()
            .AddFinTrackSecurity(new PublicAddress(new Uri("https://money.example.test/")));

        _services = services.BuildServiceProvider();
        _scope = _services.CreateAsyncScope();
    }

    [After(Test)]
    public async Task DisposeIdentity()
    {
        await _scope.DisposeAsync();
        await _services.DisposeAsync();
        _metrics.Dispose();
    }

    [Test]
    public async Task TokenProviders_AreOnlyDataProtectionAndTheSha256Authenticator()
    {
        // Act
        IdentityOptions options = _services.GetRequiredService<IOptions<IdentityOptions>>().Value;

        // Assert, no Email or Phone provider, which make HMAC-SHA1 codes
        using (Assert.Multiple())
        {
            await Assert.That(options.Tokens.ProviderMap.Keys)
                .IsEquivalentTo([TokenOptions.DefaultProvider, TokenOptions.DefaultAuthenticatorProvider]);
            await Assert.That(options.Tokens.ProviderMap[TokenOptions.DefaultAuthenticatorProvider].ProviderType)
                .IsEqualTo(typeof(TotpAuthenticatorTokenProvider));
            await Assert.That(options.Tokens.ProviderMap[TokenOptions.DefaultProvider].ProviderType)
                .IsEqualTo(typeof(DataProtectorTokenProvider<FinanceTrackerUser>));
        }
    }

    [Test]
    public async Task PasswordHash_IsPbkdf2Sha512WithTheSetIterations()
    {
        // Arrange
        IPasswordHasher<FinanceTrackerUser> hasher =
            _services.GetRequiredService<IPasswordHasher<FinanceTrackerUser>>();

        // Act, Identity's v3 format. A marker byte, then the PRF, iterations and salt length big-endian
        byte[] hash = Convert.FromBase64String(hasher.HashPassword(User("hash"), "Placeholder-Password"));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(hash[0]).IsEqualTo((byte)0x01);
            // 2 is HMAC-SHA512, Identity's KeyDerivationPrf
            await Assert.That(BinaryPrimitives.ReadUInt32BigEndian(hash.AsSpan(1))).IsEqualTo(2u);
            await Assert.That(BinaryPrimitives.ReadUInt32BigEndian(hash.AsSpan(5)))
                .IsEqualTo((uint)IdentitySecurityExtensions.PasswordHashIterations);
        }
    }

    [Test]
    public async Task UserManager_MakesA256BitAuthenticatorKey()
    {
        // Act
        string key = UserManager.GenerateNewAuthenticatorKey();

        // Assert
        await Assert.That(Base32.Decode(key).Length).IsEqualTo(AuthenticatorSettings.KeyBytes);
    }

    [Test]
    public async Task AuthenticatorCode_UsedTwice_IsTakenOnceThenRefusedAndCounted()
    {
        // Arrange
        using MetricCollector<long> reused = _metrics.Collect<long>(FinTrackMetrics.AuthenticatorCodesReusedName);
        FinanceTrackerUser user = User("codes");
        await UserManager.CreateAsync(user, "correct horse battery staple");
        await UserManager.ResetAuthenticatorKeyAsync(user);
        string key = (await UserManager.GetAuthenticatorKeyAsync(user))!;
        string code = Totp.Code(Base32.Decode(key), Totp.StepAt(DateTimeOffset.UtcNow));

        // Act
        bool first = await UserManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code);
        bool again = await UserManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first).IsTrue();
            await Assert.That(again).IsFalse();
            await Assert.That(reused.GetMeasurementSnapshot().EvaluateAsCounter()).IsEqualTo(1);
        }
    }

    [Test]
    public async Task Passkeys_BelongToThePublicHostAndAlwaysAskForAFingerprintFaceOrPin()
    {
        // Act
        IdentityPasskeyOptions options = _services.GetRequiredService<IOptions<IdentityPasskeyOptions>>().Value;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(options.ServerDomain).IsEqualTo("money.example.test");
            await Assert.That(options.UserVerificationRequirement).IsEqualTo("required");
            await Assert.That(options.ResidentKeyRequirement).IsEqualTo("required");
            await Assert.That(_services.GetRequiredService<IOptions<IdentityOptions>>().Value.Stores.SchemaVersion)
                .IsEqualTo(FinanceTrackerContext.IdentitySchemaVersion);
        }
    }

    [Test]
    public async Task CreateUser_PasswordOneShortOfTheMinimum_IsRefused()
    {
        // Act
        IdentityResult result = await UserManager.CreateAsync(User("short"),
            new string('a', PasswordRules.MinimumLength - 1));

        // Assert
        await Assert.That(result.Errors.Select(x => x.Code)).Contains("PasswordTooShort");
    }

    [Test]
    public async Task CreateUser_LongEnoughPasswordOfOneKindOfCharacter_IsAllowed()
    {
        // Act, no digit, capital or symbol, which NIST advises against requiring
        IdentityResult result = await UserManager.CreateAsync(User("long"),
            "correct horse battery staple");

        // Assert
        await Assert.That(result.Succeeded).IsTrue();
    }

    /// <summary>
    /// A new user named <paramref name="name"/>.
    /// </summary>
    private static FinanceTrackerUser User(string name) =>
        new("Placeholder", "User", $"{name}@example.com", $"{name}-user");
}

using FinanceTracker.Tokens;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Tokens;

public class TokenFailureLimiterTests
{
    [Test]
    public async Task IsBlocked_OnceAnAddressUsesUpItsRefusals_IsTrueForItOnly()
    {
        // Arrange
        using TokenFailureLimiter subject = Limiter(3);

        // Act
        subject.RecordFailure("192.0.2.1");
        subject.RecordFailure("192.0.2.1");
        bool beforeLimit = subject.IsBlocked("192.0.2.1");
        subject.RecordFailure("192.0.2.1");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(beforeLimit).IsFalse();
            await Assert.That(subject.IsBlocked("192.0.2.1")).IsTrue();
            await Assert.That(subject.IsBlocked("192.0.2.2")).IsFalse();
        }
    }

    [Test]
    public async Task IsBlocked_AnAddressWithNoRefusals_IsFalse()
    {
        // Arrange
        using TokenFailureLimiter subject = Limiter(1);

        // Act & Assert
        await Assert.That(subject.IsBlocked("192.0.2.1")).IsFalse();
    }

    private static TokenFailureLimiter Limiter(int failuresPerHour) =>
        new(Options.Create(new TokenFailureLimiterOptions { FailuresPerHour = failuresPerHour }));
}

using FinanceTracker.Contracts.Diagnostics;
using FinanceTracker.Contracts.Validation;
using FinanceTracker.Tests.Unit.Shared;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Infrastructure.Validators;

public class ClientErrorValidatorTests : UnitTestBase
{
    private readonly ClientErrorValidator _subject = new();

    [Test]
    public async Task ValidateAsync_AtTheLimits_IsValid()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(Error(ClientError.MaxMessageLength,
            ClientError.MaxStackTraceLength), CancellationToken);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    [Arguments(ClientError.MaxMessageLength + 1, 10)]
    [Arguments(10, ClientError.MaxStackTraceLength + 1)]
    [Arguments(0, 10)]
    public async Task ValidateAsync_TooLongOrNoMessage_IsRefused(int messageLength, int stackTraceLength)
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(Error(messageLength, stackTraceLength),
            CancellationToken);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
    }

    private static ClientError Error(int messageLength, int stackTraceLength) => new()
    {
        Message = new string('m', messageLength),
        StackTrace = new string('s', stackTraceLength),
        ExceptionType = "System.InvalidOperationException",
        Category = "Microsoft.AspNetCore.Components.ErrorBoundary",
        Page = "/Budgets"
    };
}

using FinanceTracker.Contracts.Notifications;
using FinanceTracker.Contracts.Validation;
using FinanceTracker.Tests.Unit.Shared;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Infrastructure.Validators;

public sealed class AddPushDeviceValidatorTests : UnitTestBase, IDisposable
{
    private readonly AddPushDeviceValidator _subject = new();
    private readonly WebPushTestBrowser _browser = new();

    public void Dispose() => _browser.Dispose();

    [Test]
    public async Task ValidateAsync_WhatABrowserGives_IsValid()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(Subscription(), CancellationToken);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    [Arguments("")]
    [Arguments("push.example.test/send/1")]
    [Arguments("ftp://push.example.test/send/1")]
    public async Task ValidateAsync_NotAWholeHttpAddress_IsInvalid(string endpoint)
    {
        // Arrange
        AddPushDevice subscription = Subscription();
        subscription.Endpoint = endpoint;

        // Act
        ValidationResult result = await _subject.ValidateAsync(subscription, CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.PropertyName)).Contains(nameof(AddPushDevice.Endpoint));
    }

    [Test]
    public async Task ValidateAsync_KeyOrSecretTheWrongSize_IsInvalid()
    {
        // Arrange, a secret where the key should be and the other way round
        AddPushDevice subscription = Subscription();
        (subscription.PublicKey, subscription.AuthSecret) = (subscription.AuthSecret, subscription.PublicKey);

        // Act
        ValidationResult result = await _subject.ValidateAsync(subscription, CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.PropertyName))
            .IsEquivalentTo([nameof(AddPushDevice.PublicKey), nameof(AddPushDevice.AuthSecret)]);
    }

    [Test]
    public async Task ValidateAsync_KeyNotBase64Url_IsInvalid()
    {
        // Arrange
        AddPushDevice subscription = Subscription();
        subscription.PublicKey = "not base64 at all!";

        // Act
        ValidationResult result = await _subject.ValidateAsync(subscription, CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.PropertyName))
            .IsEquivalentTo([nameof(AddPushDevice.PublicKey)]);
    }

    [Test]
    public async Task ValidateAsync_NameMissingOrTooLong_IsInvalid()
    {
        // Arrange
        AddPushDevice missing = Subscription();
        missing.Name = "";
        AddPushDevice tooLong = Subscription();
        tooLong.Name = new string('a', AddPushDeviceValidator.MaxNameLength + 1);

        // Act
        ValidationResult missingResult = await _subject.ValidateAsync(missing, CancellationToken);
        ValidationResult tooLongResult = await _subject.ValidateAsync(tooLong, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(missingResult.IsValid).IsFalse();
            await Assert.That(tooLongResult.IsValid).IsFalse();
        }
    }

    private AddPushDevice Subscription() => new()
    {
        Endpoint = "https://push.example.test/send/1",
        PublicKey = _browser.PublicKeyText,
        AuthSecret = _browser.AuthSecretText,
        Name = "Chrome on Android"
    };
}

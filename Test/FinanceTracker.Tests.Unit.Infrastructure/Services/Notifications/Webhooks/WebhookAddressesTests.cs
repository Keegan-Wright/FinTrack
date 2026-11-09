using System.Net;
using FinanceTracker.Infrastructure.Notifications.Webhooks;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Notifications.Webhooks;

public class WebhookAddressesTests
{
    private static readonly WebhookSettings Refusing = new();
    private static readonly WebhookSettings Allowing = new() { AllowPrivateAddresses = true };

    [Test]
    [Arguments("10.1.2.3")]
    [Arguments("172.16.0.1")]
    [Arguments("172.31.255.255")]
    [Arguments("192.168.1.10")]
    [Arguments("127.0.0.1")]
    [Arguments("169.254.169.254")]
    [Arguments("100.64.0.1")]
    [Arguments("0.0.0.0")]
    [Arguments("224.0.0.1")]
    [Arguments("::1")]
    [Arguments("fd00::1")]
    [Arguments("fe80::1")]
    [Arguments("::ffff:192.168.1.10")]
    public async Task IsPublic_PrivateOrLocal_IsNot(string address) =>
        await Assert.That(WebhookAddresses.IsPublic(IPAddress.Parse(address))).IsFalse();

    [Test]
    [Arguments("93.184.215.14")]
    [Arguments("172.32.0.1")]
    [Arguments("2606:4700::1")]
    [Arguments("::ffff:93.184.215.14")]
    public async Task IsPublic_OnTheInternet_Is(string address) =>
        await Assert.That(WebhookAddresses.IsPublic(IPAddress.Parse(address))).IsTrue();

    [Test]
    [Arguments("http://192.168.1.10:8123/api/webhook/fintrack")]
    [Arguments("http://localhost:8123/api/webhook/fintrack")]
    [Arguments("http://[::1]/hook")]
    public async Task RefusalFor_PrivateWhenRefused_SaysWhy(string url) =>
        await Assert.That(WebhookAddresses.RefusalFor(new Uri(url), Refusing))
            .IsEqualTo(WebhookAddresses.PrivateRefusal);

    [Test]
    public async Task RefusalFor_PrivateWhenAllowed_TakesIt() =>
        await Assert.That(WebhookAddresses.RefusalFor(new Uri("http://192.168.1.10:8123/hook"), Allowing))
            .IsNull();

    [Test]
    public async Task RefusalFor_AName_LeavesItForWhenItsPosted() =>
        await Assert.That(WebhookAddresses.RefusalFor(new Uri("https://hooks.example.test/fintrack"), Refusing))
            .IsNull();
}

namespace FinanceTracker.Tests.Integration.Image;

/// <summary>
/// Skips a test unless <see cref="PublishedImage.AddressVariable"/> says where the published image is running.
/// Decided when the test is found, before its browser starts, so a run without the image doesn't need one.
/// </summary>
public sealed class SkipWithoutImageAttribute()
    : SkipAttribute($"Set {PublishedImage.AddressVariable} to where the published image runs, e.g. http://localhost:8080")
{
    public override Task<bool> ShouldSkip(TestRegisteredContext context) =>
        Task.FromResult(PublishedImage.Address is null);
}

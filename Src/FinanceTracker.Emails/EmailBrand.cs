namespace FinanceTracker.Emails;

/// <summary>
/// What makes an email FinTrack's the logo, carried inside each email so it shows without loading anything.
/// </summary>
public static class EmailBrand
{
    /// <summary>
    /// What an email's HTML calls the logo, <c>src="cid:fintrack-logo"</c>.
    /// </summary>
    public const string LogoContentId = "fintrack-logo";

    private static readonly Lazy<MailImage> LazyLogo = new(LoadLogo);

    /// <summary>
    /// The logo, the favicon as a PNG, see the Emails project file.
    /// </summary>
    public static MailImage Logo => LazyLogo.Value;

    private static MailImage LoadLogo()
    {
        using Stream stream = typeof(EmailBrand).Assembly.GetManifestResourceStream("Email.logo.png")
                              ?? throw new InvalidOperationException("The email logo isn't embedded.");
        using MemoryStream copy = new();
        stream.CopyTo(copy);
        return new MailImage(LogoContentId, "fintrack.png", "image/png", copy.ToArray());
    }
}

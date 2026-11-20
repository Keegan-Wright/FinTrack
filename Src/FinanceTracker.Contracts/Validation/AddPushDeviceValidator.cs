using System.Buffers.Text;
using FinanceTracker.Contracts.Notifications;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires what a browser gives when it subscribes. A whole http or https address, a P-256 key and a 16-byte secret
/// in base64url, and a short name.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<AddPushDevice>>]
public class AddPushDeviceValidator : AbstractValidator<AddPushDevice>
{
    /// <summary>
    /// The longest address taken.
    /// </summary>
    public const int MaxEndpointLength = 2000;

    /// <summary>
    /// The longest name taken.
    /// </summary>
    public const int MaxNameLength = 60;

    /// <summary>
    /// An uncompressed P-256 public key's length.
    /// </summary>
    private const int PublicKeyLength = 65;

    /// <summary>
    /// The auth secret's length.
    /// </summary>
    private const int AuthSecretLength = 16;

    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public AddPushDeviceValidator()
    {
        RuleFor(x => x.Endpoint)
            .NotEmpty().WithMessage("The browser gave no push address")
            .MaximumLength(MaxEndpointLength).WithMessage($"The push address is over {MaxEndpointLength} characters")
            .Must(BeHttp).WithMessage("The push address isn't a whole http or https address");

        RuleFor(x => x.PublicKey)
            .Must(x => DecodedLength(x) == PublicKeyLength).WithMessage("The browser's key isn't a P-256 key");

        RuleFor(x => x.AuthSecret)
            .Must(x => DecodedLength(x) == AuthSecretLength).WithMessage("The browser's secret isn't 16 bytes");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("The device needs a name")
            .MaximumLength(MaxNameLength).WithMessage($"Keep the name under {MaxNameLength} characters");
    }

    private static bool BeHttp(string url) =>
        Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri? parsed)
        && (parsed.Scheme == Uri.UriSchemeHttps || parsed.Scheme == Uri.UriSchemeHttp)
        && !string.IsNullOrEmpty(parsed.Host);

    /// <summary>
    /// How many bytes <paramref name="value"/> decodes to, -1 when it isn't base64url.
    /// </summary>
    /// <param name="value">The base64url text.</param>
    private static int DecodedLength(string value)
    {
        if (string.IsNullOrEmpty(value) || !Base64Url.IsValid(value, out int length))
        {
            return -1;
        }

        return length;
    }
}

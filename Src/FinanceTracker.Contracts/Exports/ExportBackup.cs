using System.ComponentModel;

namespace FinanceTracker.Contracts.Exports;

/// <summary>
/// How to make the backup, plain or protected with a password.
/// </summary>
public class ExportBackup
{
    /// <summary>
    /// The password the zip is encrypted with, AES-256, or null for a plain zip. Never stored, see
    /// <see cref="BackupPasswordRules"/>.
    /// </summary>
    [Description("The password to encrypt the zip with, or null for a plain zip")]
    public string? Password { get; set; }

    /// <summary>
    /// <see cref="Password"/> typed again, so a typo doesn't lock the user out of their own backup.
    /// </summary>
    [Description("The password typed again")]
    public string? ConfirmPassword { get; set; }
}

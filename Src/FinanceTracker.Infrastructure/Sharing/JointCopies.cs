namespace FinanceTracker.Infrastructure.Sharing;

/// <summary>
/// A joint account's two copies and whose each is, see <see cref="Domain.Sharing.JointAccount"/>.
/// </summary>
/// <param name="HiddenId">The copy that's hidden, its transactions only showing when they fill a gap.</param>
/// <param name="HiddenOwner">Who connected the hidden copy.</param>
/// <param name="ShownId">The copy both people see.</param>
/// <param name="ShownOwner">Who connected the shown copy.</param>
internal sealed record JointCopies(Guid HiddenId, Guid HiddenOwner, Guid ShownId, Guid ShownOwner);

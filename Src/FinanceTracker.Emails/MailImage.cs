namespace FinanceTracker.Emails;

/// <summary>
/// An image carried inside an email and shown in its HTML as <c>cid:{ContentId}</c>, e.g. the logo, so it shows
/// without loading anything from the internet.
/// </summary>
/// <param name="ContentId">What the HTML calls it, <c>src="cid:{ContentId}"</c>.</param>
/// <param name="FileName">Its name as an attachment, for email apps that list it.</param>
/// <param name="ContentType">Its media type, e.g. <c>image/png</c>.</param>
public sealed record MailImage(string ContentId, string FileName, string ContentType, ReadOnlyMemory<byte> Content);

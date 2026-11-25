using Microsoft.JSInterop;

namespace FinanceTracker.Client.Common.Extensions;

/// <summary>
/// Saves files in the browser.
/// </summary>
public static class FileDownloadExtensions
{
    /// <summary>
    /// The script function that saves the file, in the app's <c>js/app.js</c>.
    /// </summary>
    internal const string DownloadFunction = "ftDownloadFile";

    extension(IJSRuntime js)
    {
        /// <summary>
        /// Saves <paramref name="content"/> in the browser as <paramref name="fileName"/>, as if it had been
        /// downloaded.
        /// </summary>
        /// <param name="fileName">The file's name.</param>
        /// <param name="contentType">The file's media type.</param>
        /// <param name="content">The file's bytes.</param>
        /// <param name="cancellationToken">Stops the work.</param>
        /// <returns>Whether the browser saved it, so the caller can say when it didn't.</returns>
        public async Task<bool> SaveFileAsync(string fileName, string contentType, Stream content,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(content);

            using DotNetStreamReference reference = new(content, leaveOpen: true);
            try
            {
                await js.InvokeVoidAsync(DownloadFunction, cancellationToken, fileName, contentType, reference);
                return true;
            }
            catch (JSException)
            {
                return false;
            }
        }
    }
}

using Microsoft.JSInterop;

namespace FinanceTracker.Client.Extensions;

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
        public async Task SaveFileAsync(string fileName, string contentType, Stream content,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(content);

            using DotNetStreamReference reference = new(content, leaveOpen: true);
            await js.InvokeVoidAsync(DownloadFunction, cancellationToken, fileName, contentType, reference);
        }
    }
}

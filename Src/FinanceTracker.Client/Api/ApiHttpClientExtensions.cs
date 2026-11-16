using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using FinanceTracker.Contracts;

namespace FinanceTracker.Client.Api;

/// <summary>
/// The calls every API client makes, translating the API's status codes back into what the service interfaces
/// promise.
/// </summary>
public static class ApiHttpClientExtensions
{
    extension(HttpClient http)
    {
        /// <summary>
        /// GETs a JSON array, yielding each item as it arrives rather than once the whole array has downloaded.
        /// </summary>
        public IAsyncEnumerable<T> GetStreamAsync<T>(string path, CancellationToken cancellationToken) =>
            http.StreamAsync<T>(new HttpRequestMessage(HttpMethod.Get, path), cancellationToken);

        /// <summary>
        /// POSTs <paramref name="body"/> and streams back a JSON array, see <see cref="GetStreamAsync{T}"/>.
        /// </summary>
        public IAsyncEnumerable<T> PostStreamAsync<TBody, T>(string path, TBody body,
            CancellationToken cancellationToken) =>
            http.StreamAsync<T>(new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) },
                cancellationToken);

        /// <summary>
        /// POSTs <paramref name="body"/> and reads back the JSON response.
        /// </summary>
        /// <exception cref="HttpRequestException">The API didn't answer with success.</exception>
        public async Task<T> PostReadAsync<TBody, T>(string path, TBody body, CancellationToken cancellationToken)
        {
            using HttpResponseMessage response = await http.PostAsJsonAsync(path, body, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.ReadRequiredJsonAsync<T>(cancellationToken);
        }

        /// <summary>
        /// DELETEs <paramref name="path"/>, for the services' delete methods that return whether anything was found.
        /// </summary>
        /// <returns><see langword="false"/> when the API answers 404.</returns>
        public async Task<bool> DeleteIfFoundAsync(string path, CancellationToken cancellationToken)
        {
            using HttpResponseMessage response = await http.DeleteAsync(path, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }

            response.EnsureSuccessStatusCode();
            return true;
        }

        private async IAsyncEnumerable<T> StreamAsync<T>(HttpRequestMessage request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            using (request)
            {
                using HttpResponseMessage response = await http.SendAsync(request,
                    HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                await foreach (T? item in response.Content.ReadFromJsonAsAsyncEnumerable<T>(cancellationToken))
                {
                    if (item is not null)
                    {
                        yield return item;
                    }
                }
            }
        }
    }

    extension(HttpResponseMessage response)
    {
        /// <summary>
        /// For the services that throw when a record is missing or isn't the user's, which the API answers with 404, or
        /// when one of the app's rules refuses the change, which it answers with 422. Throws as the service would.
        /// </summary>
        /// <exception cref="NotFoundException">The API answered 404.</exception>
        /// <exception cref="RuleBrokenException">The API answered 422.</exception>
        /// <exception cref="HttpRequestException">The API answered with any other failure.</exception>
        public void EnsureFound()
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new NotFoundException($"{response.RequestMessage?.RequestUri} was not found.");
            }

            if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                throw new RuleBrokenException($"{response.RequestMessage?.RequestUri} was refused by one of the rules.");
            }

            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// For the services that also throw <see cref="NameTakenException"/> when the user already has something
        /// called that, which the API answers with 409.
        /// </summary>
        /// <param name="kind">What's named, e.g. <c>savings goal</c>.</param>
        /// <param name="name">The name that was sent.</param>
        /// <exception cref="NameTakenException">The API answered 409.</exception>
        /// <exception cref="InvalidOperationException">The API answered 404.</exception>
        /// <exception cref="HttpRequestException">The API answered with any other failure.</exception>
        public void EnsureNameFree(string kind, string name)
        {
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                throw new NameTakenException(kind, name.Trim());
            }

            response.EnsureFound();
        }

        /// <summary>
        /// Reads the JSON body, which the API always sends for the endpoints this is used on.
        /// </summary>
        public async Task<T> ReadRequiredJsonAsync<T>(CancellationToken cancellationToken) =>
            await response.Content.ReadFromJsonAsync<T>(cancellationToken)
            ?? throw new InvalidOperationException($"{response.RequestMessage?.RequestUri} returned no body.");
    }
}

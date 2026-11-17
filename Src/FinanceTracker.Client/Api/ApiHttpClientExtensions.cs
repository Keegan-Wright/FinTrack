using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
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
        /// <param name="path">The API path.</param>
        /// <param name="cancellationToken">Stops the call.</param>
        public IAsyncEnumerable<T> GetStreamAsync<T>(string path, CancellationToken cancellationToken) =>
            http.StreamAsync<T>(new HttpRequestMessage(HttpMethod.Get, path), cancellationToken);

        /// <summary>
        /// POSTs <paramref name="body"/> and streams back a JSON array, see <see cref="GetStreamAsync{T}"/>.
        /// </summary>
        /// <param name="path">The API path.</param>
        /// <param name="body">What's sent.</param>
        /// <param name="cancellationToken">Stops the call.</param>
        public IAsyncEnumerable<T> PostStreamAsync<TBody, T>(string path, TBody body,
            CancellationToken cancellationToken) =>
            http.StreamAsync<T>(new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) },
                cancellationToken);

        /// <summary>
        /// POSTs <paramref name="body"/> and reads back the JSON response.
        /// </summary>
        /// <param name="path">The API path.</param>
        /// <param name="body">What's sent.</param>
        /// <param name="cancellationToken">Stops the call.</param>
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
        /// <param name="path">The API path.</param>
        /// <param name="cancellationToken">Stops the call.</param>
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

        /// <summary>
        /// Sends <paramref name="request"/> and reads the answer's JSON array an item at a time, as it arrives.
        /// </summary>
        /// <typeparam name="T">What the array holds.</typeparam>
        /// <param name="request">The request, disposed once read.</param>
        /// <param name="cancellationToken">Stops the call.</param>
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
        /// <param name="cancellationToken">Stops the call.</param>
        /// <exception cref="NotFoundException">The API answered 404.</exception>
        /// <exception cref="RuleBrokenException">The API answered 422, with the rule's own message, e.g. <c>Only your
        /// own accounts can be shared.</c></exception>
        /// <exception cref="HttpRequestException">The API answered with any other failure.</exception>
        public async Task EnsureFoundAsync(CancellationToken cancellationToken)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new NotFoundException($"{response.RequestMessage?.RequestUri} was not found.");
            }

            if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                throw new RuleBrokenException(await response.RuleMessageAsync(cancellationToken));
            }

            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// For the services that also throw <see cref="NameTakenException"/> when the user already has something
        /// called that, which the API answers with 409.
        /// </summary>
        /// <param name="kind">What's named, e.g. <c>savings goal</c>.</param>
        /// <param name="name">The name that was sent.</param>
        /// <param name="cancellationToken">Stops the call.</param>
        /// <exception cref="NameTakenException">The API answered 409.</exception>
        /// <exception cref="NotFoundException">The API answered 404.</exception>
        /// <exception cref="RuleBrokenException">The API answered 422.</exception>
        /// <exception cref="HttpRequestException">The API answered with any other failure.</exception>
        public async Task EnsureNameFreeAsync(string kind, string name, CancellationToken cancellationToken)
        {
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                throw new NameTakenException(kind, name.Trim());
            }

            await response.EnsureFoundAsync(cancellationToken);
        }

        /// <summary>
        /// The message of the rule that refused the change, the <c>detail</c> of the problem the API answers 422 with.
        /// A general one when the body has none, so the user is still told something.
        /// </summary>
        /// <param name="cancellationToken">Stops the call.</param>
        private async Task<string> RuleMessageAsync(CancellationToken cancellationToken)
        {
            string fallback = $"{response.RequestMessage?.RequestUri} was refused by one of the rules.";
            try
            {
                JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
                if (problem.ValueKind == JsonValueKind.Object &&
                    problem.TryGetProperty("detail", out JsonElement detail) &&
                    detail.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(detail.GetString()))
                {
                    return detail.GetString()!;
                }
            }
            catch (JsonException)
            {
                // Not a problem body, the general message does
            }

            return fallback;
        }

        /// <summary>
        /// Reads the JSON body, which the API always sends for the endpoints this is used on.
        /// </summary>
        /// <param name="cancellationToken">Stops the call.</param>
        public async Task<T> ReadRequiredJsonAsync<T>(CancellationToken cancellationToken) =>
            await response.Content.ReadFromJsonAsync<T>(cancellationToken)
            ?? throw new InvalidOperationException($"{response.RequestMessage?.RequestUri} returned no body.");
    }
}

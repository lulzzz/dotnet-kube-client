using HTTPlease;
using HTTPlease.Formatters.Json;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KubeClient.ResourceClients
{
    using Models;

    /// <summary>
    ///     The base class for Kubernetes resource API clients.
    /// </summary>
    public abstract class KubeResourceClient
    {
        /// <summary>
        ///     The default buffer size to use when streaming data from the Kubernetes API.
        /// </summary>
        protected const int DefaultStreamingBufferSize = 2048;

        /// <summary>
        ///     The media type used to indicate that request is a Kubernetes PATCH request.
        /// </summary>
        protected static readonly string PatchMediaType = "application/json-patch+json";

        /// <summary>
        ///     The media type used to indicate that request is a Kubernetes merge-style PATCH request.
        /// </summary>
        protected static readonly string MergePatchMediaType = "application/merge-patch+json";

        /// <summary>
        ///     JSON serialisation settings.
        /// </summary>
        protected internal static JsonSerializerSettings SerializerSettings => new JsonSerializerSettings
        {
            Converters =
            {
                new StringEnumConverter()
            }
        };

        /// <summary>
        ///     The factory for Kubernetes API requests.
        /// </summary>
        protected static HttpRequestFactory KubeRequest { get; } = new HttpRequestFactory(
            HttpRequest.Empty.ExpectJson().WithFormatter(new JsonFormatter
            {
                SerializerSettings = SerializerSettings,
                SupportedMediaTypes =
                {
                    PatchMediaType,
                    MergePatchMediaType
                }
            })
        );

        /// <summary>
        ///     Create a new <see cref="KubeResourceClient"/>.
        /// </summary>
        /// <param name="client">
        ///     The Kubernetes API client.
        /// </param>
        protected KubeResourceClient(KubeApiClient client)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));
            
            KubeClient = client;
        }

        /// <summary>
        ///     The Kubernetes API client.
        /// </summary>
        public KubeApiClient KubeClient { get; }

        /// <summary>
        ///     The underlying HTTP client.
        /// </summary>
        protected HttpClient Http => KubeClient.Http;

        /// <summary>
        ///     An <see cref="ILoggerFactory"/> used to create loggers for client components.
        /// </summary>
        protected ILoggerFactory LoggerFactory => KubeClient.LoggerFactory;

        /// <summary>
        ///     Get a single resource, returning <c>null</c> if it does not exist.
        /// </summary>
        /// <typeparam name="TResource">
        ///     The type of resource to retrieve.
        /// </typeparam>
        /// <param name="request">
        ///     An <see cref="HttpRequest"/> representing the resource to retrieve.
        /// </param>
        /// <param name="cancellationToken">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the request.
        /// </param>
        /// <returns>
        ///     A <typeparamref name="TResource"/> representing the current state for the resource, or <c>null</c> if no resource was found with the specified name and namespace.
        /// </returns>
        protected async Task<TResource> GetSingleResource<TResource>(HttpRequest request, CancellationToken cancellationToken = default)
            where TResource : KubeResourceV1
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            
            using (HttpResponseMessage responseMessage = await Http.GetAsync(request, cancellationToken))
            {
                if (responseMessage.IsSuccessStatusCode)
                    return await responseMessage.ReadContentAsAsync<TResource>();

                // Ensure that HttpStatusCode.NotFound actually refers to the target resource.
                StatusV1 status = await responseMessage.ReadContentAsAsync<StatusV1, StatusV1>(HttpStatusCode.NotFound);
                if (status.Reason == "NotFound")
                    return null;

                // If possible, tell the consumer which resource type we had a problem with (helpful when all you find is the error message in the log).
                (string itemKind, string itemApiVersion) = KubeObjectV1.GetKubeKind<TResource>();
                string resourceTypeDescription =
                    !String.IsNullOrWhiteSpace(itemKind)
                        ? $"{itemKind} ({itemApiVersion}) resource"
                        : typeof(TResource).Name;

                throw new KubeClientException($"Failed to retrieve {resourceTypeDescription} (HTTP status {responseMessage.StatusCode}).",
                    innerException: new HttpRequestException<StatusV1>(responseMessage.StatusCode,
                        response: await responseMessage.ReadContentAsAsync<StatusV1, StatusV1>()
                    )
                );
            }
        }

        /// <summary>
        ///     Get a list of resources.
        /// </summary>
        /// <typeparam name="TResourceList">
        ///     The type of resource list to retrieve.
        /// </typeparam>
        /// <param name="request">
        ///     An <see cref="HttpRequest"/> representing the resource to retrieve.
        /// </param>
        /// <param name="cancellationToken">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the request.
        /// </param>
        /// <returns>
        ///     A <typeparamref name="TResourceList"/> containing the resources.
        /// </returns>
        protected async Task<TResourceList> GetResourceList<TResourceList>(HttpRequest request, CancellationToken cancellationToken = default)
            where TResourceList : KubeResourceListV1
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            
            using (HttpResponseMessage responseMessage = await Http.GetAsync(request, cancellationToken))
            {
                if (responseMessage.IsSuccessStatusCode)
                    return await responseMessage.ReadContentAsAsync<TResourceList>();

                // If possible, tell the consumer which resource type we had a problem with (helpful when all you find is the error message in the log).
                (string itemKind, string itemApiVersion) = KubeResourceListV1.GetListItemKubeKind<TResourceList>();
                string resourceTypeDescription =
                    !String.IsNullOrWhiteSpace(itemKind)
                        ? $"{itemKind} ({itemApiVersion}) resources"
                        : typeof(TResourceList).Name;

                throw new KubeClientException($"Failed to list {resourceTypeDescription} (HTTP status {responseMessage.StatusCode}).",
                    innerException: new HttpRequestException<StatusV1>(responseMessage.StatusCode,
                        response: await responseMessage.ReadContentAsAsync<StatusV1, StatusV1>()
                    )
                );
            }
        }

        /// <summary>
        ///     Perform a JSON patch operation on a Kubernetes resource.
        /// </summary>
        /// <typeparam name="TResource">
        ///     The target resource type.
        /// </typeparam>
        /// <param name="patchAction">
        ///     A delegate that performs customisation of the patch operation.
        /// </param>
        /// <param name="request">
        ///     An <see cref="HttpRequest"/> representing the patch request.
        /// </param>
        /// <param name="cancellationToken">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the request.
        /// </param>
        /// <returns>
        ///     A <typeparamref name="TResource"/> representing the updated resource.
        /// </returns>
        protected Task<TResource> PatchResource<TResource>(Action<JsonPatchDocument<TResource>> patchAction, HttpRequest request, CancellationToken cancellationToken)
            where TResource : KubeResourceV1
        {
            if (patchAction == null)
                throw new ArgumentNullException(nameof(patchAction));
            
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            
            var patch = new JsonPatchDocument<TResource>();
            patchAction(patch);

            return Http
                .PatchAsync(request,
                    patchBody: patch,
                    mediaType: PatchMediaType,
                    cancellationToken: cancellationToken
                )
                .ReadContentAsAsync<TResource, StatusV1>();
        }

        /// <summary>
        ///     Perform a JSON patch operation on a Kubernetes resource.
        /// </summary>
        /// <typeparam name="TResource">
        ///     The target resource type.
        /// </typeparam>
        /// <param name="patchAction">
        ///     A delegate that performs customisation of the patch operation.
        /// </param>
        /// <param name="request">
        ///     An <see cref="HttpRequest"/> representing the patch request.
        /// </param>
        /// <param name="cancellationToken">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the request.
        /// </param>
        /// <returns>
        ///     A <typeparamref name="TResource"/> representing the updated resource.
        /// </returns>
        protected Task<TResource> PatchResourceRaw<TResource>(Action<JsonPatchDocument> patchAction, HttpRequest request, CancellationToken cancellationToken)
            where TResource : KubeResourceV1
        {
            if (patchAction == null)
                throw new ArgumentNullException(nameof(patchAction));
            
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            
            var patch = new JsonPatchDocument();
            patchAction(patch);

            return Http
                .PatchAsync(request,
                    patchBody: patch,
                    mediaType: PatchMediaType,
                    cancellationToken: cancellationToken
                )
                .ReadContentAsAsync<TResource, StatusV1>();
        }

        /// <summary>
        ///     Get an <see cref="IObservable{T}"/> for <see cref="IResourceEventV1{TResource}"/>s streamed from an HTTP GET request.
        /// </summary>
        /// <typeparam name="TResource">
        ///     The resource type that the events relate to.
        /// </typeparam>
        /// <param name="request">
        ///     The <see cref="HttpRequest"/> to execute.
        /// </param>
        /// <returns>
        ///     The <see cref="IObservable{T}"/>.
        /// </returns>
        protected IObservable<IResourceEventV1<TResource>> ObserveEvents<TResource>(HttpRequest request)
            where TResource : KubeResourceV1
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            return ObserveLines(request).Select(
                line => (IResourceEventV1<TResource>)JsonConvert.DeserializeObject<ResourceEventV1<TResource>>(line, SerializerSettings)
            );
        }

        /// <summary>
        ///     Get an <see cref="IObservable{T}"/> for lines streamed from an HTTP GET request.
        /// </summary>
        /// <param name="request">
        ///     The <see cref="HttpRequest"/> to execute.
        /// </param>
        /// <param name="bufferSize">
        ///     The buffer size to use when streaming data.
        /// 
        ///     Default is 2048 bytes.
        /// </param>
        /// <returns>
        ///     The <see cref="IObservable{T}"/>.
        /// </returns>
        protected IObservable<string> ObserveLines(HttpRequest request, int bufferSize = DefaultStreamingBufferSize)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            
            return Observable.Create<string>(async (subscriber, cancellationToken) =>
            {
                try
                {
                    using (HttpResponseMessage responseMessage = await Http.GetStreamedAsync(request, cancellationToken))
                    {
                        if (!responseMessage.IsSuccessStatusCode)
                        {
                            throw HttpRequestException<StatusV1>.Create(responseMessage.StatusCode,
                                await responseMessage.ReadContentAsAsync<StatusV1, StatusV1>()
                            );
                        }

                        MediaTypeHeaderValue contentTypeHeader = responseMessage.Content.Headers.ContentType;
                        if (contentTypeHeader == null)
                            throw new KubeClientException("Response is missing 'Content-Type' header.");

                        Encoding encoding =
                            !String.IsNullOrWhiteSpace(contentTypeHeader.CharSet)
                                ? Encoding.GetEncoding(contentTypeHeader.CharSet)
                                : Encoding.UTF8;

                        Decoder decoder = encoding.GetDecoder();

                        using (Stream responseStream = await responseMessage.Content.ReadAsStreamAsync())
                        {
                            StringBuilder lineBuilder = new StringBuilder();
                            
                            byte[] buffer = new byte[bufferSize];
                            int bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                            while (bytesRead > 0)
                            {
                                // AF: Slightly inefficient because we wind up scanning the buffer twice.
                                char[] decodedCharacters = new char[decoder.GetCharCount(buffer, 0, bytesRead)];
                                int charactersDecoded = decoder.GetChars(buffer, 0, bytesRead, decodedCharacters, 0);
                                for (int charIndex = 0; charIndex < charactersDecoded; charIndex++)
                                {
                                    const char CR = '\r';
                                    const char LF = '\n';

                                    char decodedCharacter = decodedCharacters[charIndex];
                                    switch (decodedCharacter)
                                    {
                                        case CR:
                                        {
                                            if (charIndex < charactersDecoded - 1 && decodedCharacters[charIndex + 1] == LF)
                                            {
                                                charIndex++;

                                                goto case LF;
                                            }

                                            break;
                                        }
                                        case LF:
                                        {
                                            string line = lineBuilder.ToString();
                                            lineBuilder.Clear();

                                            subscriber.OnNext(line);

                                            break;
                                        }
                                        default:
                                        {
                                            lineBuilder.Append(decodedCharacter);

                                            break;
                                        }
                                    }
                                }

                                bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                            }

                            // If stream doesn't end with a line-terminator sequence, publish trailing characters as the last line.
                            if (lineBuilder.Length > 0)
                            {
                                subscriber.OnNext(
                                    lineBuilder.ToString()
                                );
                            }
                        }
                    }
                }
                catch (OperationCanceledException operationCanceled) when (operationCanceled.CancellationToken != cancellationToken)
                {
                    if (!cancellationToken.IsCancellationRequested) // Don't bother publishing if subscriber has already disconnected.
                        subscriber.OnError(operationCanceled);
                }
                catch (Exception exception)
                {
                    if (!cancellationToken.IsCancellationRequested) // Don't bother publishing if subscriber has already disconnected.
                        subscriber.OnError(exception);
                }
                finally
                {
                    if (!cancellationToken.IsCancellationRequested) // Don't bother publishing if subscriber has already disconnected.
                        subscriber.OnCompleted();
                }
            });
        }
    }
}

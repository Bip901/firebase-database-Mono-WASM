namespace Firebase.Database.Query
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Firebase.Database.Http;
    using Firebase.Database.Streaming;

    using Newtonsoft.Json;
    using System.Net;
    using Firebase.Observables;
    using System.Threading;

    /// <summary>
    /// Represents a firebase query. 
    /// </summary>
    public abstract class FirebaseQuery : IFirebaseQuery, IDisposable
    {
        protected TimeSpan DEFAULT_HTTP_CLIENT_TIMEOUT = new TimeSpan(0, 0, 180);

        protected readonly FirebaseQuery Parent;

        private IHttpClientProxy client;

        /// <summary> 
        /// Initializes a new instance of the <see cref="FirebaseQuery"/> class.
        /// </summary>
        /// <param name="parent"> The parent of this query. </param>
        /// <param name="client"> The owning client. </param>
        protected FirebaseQuery(FirebaseQuery parent, FirebaseClient client)
        {
            this.Client = client;
            this.Parent = parent;
        }

        /// <summary>
        /// Gets the client.
        /// </summary>
        public FirebaseClient Client
        {
            get;
        }

        /// <summary>
        /// Queries the firebase server once returning collection of items.
        /// </summary>
        /// <param name="timeout"> Optional timeout value. </param>
        /// <param name="cancellationToken"> Optional cancellation token. </param>
        /// <typeparam name="T"> Type of elements. </typeparam>
        /// <returns> Collection of <see cref="FirebaseObject{T}"/> holding the entities returned by server. </returns>
        public async Task<IReadOnlyCollection<FirebaseObject<T>>> OnceAsync<T>(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var url = string.Empty;

            try
            {
                url = await this.BuildUrlAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new FirebaseException("Couldn't build the url", string.Empty, string.Empty, HttpStatusCode.OK, ex);
            }

            return await this.GetClient(timeout).GetObjectDictionaryCollectionAsync<T>(url, Client.Options.JsonSerializerSettings, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Assumes given query is pointing to a list of object of type <typeparamref name="T"/> and retrieves it.
        /// </summary>
        /// <param name="timeout"> Optional timeout value. </param>
        /// <param name="cancellationToken"> Optional cancellation token. </param>
        /// <typeparam name="T"> Type of elements. </typeparam>
        /// <returns> Single object of type <typeparamref name="T"/>. </returns>
        public async Task<IReadOnlyCollection<FirebaseObject<T>>> OnceAsListAsync<T>(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var url = string.Empty;

            try
            {
                url = await this.BuildUrlAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new FirebaseException("Couldn't build the url", string.Empty, string.Empty, HttpStatusCode.OK, ex);
            }

            return await this.GetClient(timeout).GetObjectCollectionAsync<T>(url, Client.Options.JsonSerializerSettings, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Assumes given query is pointing to a single object of type <typeparamref name="T"/> and retrieves it.
        /// </summary>
        /// <param name="timeout"> Optional timeout value. </param>
        /// <param name="cancellationToken"> Cancellation token. </param>
        /// <typeparam name="T"> Type of elements. </typeparam>
        /// <returns> Single object of type <typeparamref name="T"/>. </returns>
        public async Task<T> OnceSingleAsync<T>(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {

            var responseData = string.Empty;
            var statusCode = HttpStatusCode.OK;
            var url = string.Empty;

            try
            {
                url = await this.BuildUrlAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new FirebaseException("Couldn't build the url", string.Empty, responseData, statusCode, ex);
            }

            try
            {
                var response = await this.GetClient(timeout).GetAsync(url, cancellationToken).ConfigureAwait(false);
                statusCode = response.StatusCode;
                responseData = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                response.EnsureSuccessStatusCode();
                response.Dispose();

                return JsonConvert.DeserializeObject<T>(responseData, Client.Options.JsonSerializerSettings);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new FirebaseException(url, string.Empty, responseData, statusCode, ex);
            }
        }

        /// <summary>
        /// Returns the response data as json string.
        /// </summary>
        /// <param name="timeout"> Optional timeout value. </param>
        /// <param name="cancellationToken"> Cancellation token. </param>
        public async Task<string> OnceAsJsonAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var responseData = string.Empty;
            var statusCode = HttpStatusCode.OK;
            var url = string.Empty;

            try
            {
                url = await this.BuildUrlAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new FirebaseException("Couldn't build the url", string.Empty, responseData, statusCode, ex);
            }

            try
            {
                var response = await this.GetClient(timeout).GetAsync(url, cancellationToken).ConfigureAwait(false);
                statusCode = response.StatusCode;
                responseData = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                response.EnsureSuccessStatusCode();
                response.Dispose();

                return responseData;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new FirebaseException(url, string.Empty, responseData, statusCode, ex);
            }
        }

        /// <summary>
        /// Starts observing this query watching for changes real time sent by the server.
        /// </summary>
        /// <typeparam name="T"> Type of elements. </typeparam>
        /// <param name="exceptionHandler"> Optional exception handler for the stream subscription. </param>
        /// <param name="elementRoot"> Optional custom root element of received json items. </param>
        /// <returns> Observable stream of <see cref="FirebaseEvent{T}"/>. </returns>
        public IObservable<FirebaseEvent<T>> AsObservable<T>(EventHandler<ContinueExceptionEventArgs<FirebaseException>> exceptionHandler = null, string elementRoot = "")
        {
            return AnonymousObservable<FirebaseEvent<T>>.Create(observer =>
            {
                var sub = new FirebaseSubscription<T>(observer, this, elementRoot, new FirebaseCache<T>(Client.Options.JsonSerializerSettings));
                sub.ExceptionThrown += exceptionHandler;
                return sub.Run();
            });
        }

        /// <summary>
        /// Builds the actual URL of this query.
        /// </summary>
        /// <returns> The <see cref="string"/>. </returns>
        public async Task<string> BuildUrlAsync()
        {
            // if token factory is present on the parent then use it to generate auth token
            if (this.Client.Options.AuthTokenAsyncFactory != null)
            {
                var token = await this.Client.Options.AuthTokenAsyncFactory().ConfigureAwait(false);
                return this.WithAuth(token).BuildUrl(null);
            }

            return this.BuildUrl(null);
        }

        /// <summary>
        /// Posts given object to repository.
        /// </summary>
        /// <param name="data"> The json data. </param>
        /// <param name="generateKeyOffline"> Specifies whether the key should be generated offline instead of online. </param>
        /// <param name="timeout"> Optional timeout value. </param>
        /// <param name="cancellationToken"> Optional cancellation token. </param>
        /// <returns> Resulting firebase object with populated key. </returns>
        public async Task<FirebaseObject<string>> PostAsync(string data, bool generateKeyOffline = true, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            // post generates a new key server-side, while put can be used with an already generated local key
            if (generateKeyOffline)
            {
                var key = FirebaseKeyGenerator.Next();
                await new ChildQuery(this, () => key, this.Client).PutAsync(data, timeout, cancellationToken).ConfigureAwait(false);

                return new FirebaseObject<string>(key, data);
            }
            else
            {
                var c = this.GetClient(timeout);
                var sendData = await this.SendAsync(c, data, HttpMethod.Post, cancellationToken).ConfigureAwait(false);
                var result = JsonConvert.DeserializeObject<PostResult>(sendData, Client.Options.JsonSerializerSettings);

                return new FirebaseObject<string>(result.Name, data);
            }
        }

        /// <summary>
        /// Patches data at given location instead of overwriting them.
        /// </summary> 
        /// <param name="data"> The json data. </param>
        /// <param name="timeout"> Optional timeout value. </param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns> The <see cref="Task"/>. </returns>
        public Task PatchAsync(string data, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var c = this.GetClient(timeout);

            return this.Silent().SendAsync(c, data, HttpMethod.Patch, cancellationToken);
        }

        /// <summary>
        /// Sets or overwrites data at given location.
        /// </summary> 
        /// <param name="data"> The json data. </param>
        /// <param name="timeout"> Optional timeout value. </param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns> The <see cref="Task"/>. </returns>
        public Task PutAsync(string data, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var c = this.GetClient(timeout);

            return this.Silent().SendAsync(c, data, HttpMethod.Put, cancellationToken);
        }

        /// <summary>
        /// Deletes data from given location.
        /// </summary>
        /// <param name="timeout"> Optional timeout value. </param>
        /// <param name="cancellationToken"> Optional cancellation token. </param>
        /// <returns> The <see cref="Task"/>. </returns>
        public async Task DeleteAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var c = this.GetClient(timeout);
            var url = string.Empty;
            var responseData = string.Empty;
            var statusCode = HttpStatusCode.OK;

            try
            {
                url = await this.BuildUrlAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new FirebaseException("Couldn't build the url", string.Empty, responseData, statusCode, ex);
            }

            try
            {
                var result = await c.DeleteAsync(url, cancellationToken).ConfigureAwait(false);
                statusCode = result.StatusCode;
                responseData = await result.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                result.EnsureSuccessStatusCode();
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new FirebaseException(url, string.Empty, responseData, statusCode, ex);
            }
        }

        /// <summary>
        /// Disposes this instance.  
        /// </summary>
        public void Dispose()
        {
            this.client?.Dispose();
        }

        /// <summary>
        /// Build the url segment of this child.
        /// </summary>
        /// <param name="child"> The child of this query. </param>
        /// <returns> The <see cref="string"/>. </returns>
        protected abstract string BuildUrlSegment(FirebaseQuery child);

        private string BuildUrl(FirebaseQuery child)
        {
            var url = this.BuildUrlSegment(child);

            if (this.Parent != null)
            {
                url = this.Parent.BuildUrl(this) + url;
            }

            return url;
        }

        private IHttpClient GetClient(TimeSpan? timeout = null)
        {
            if (this.client == null)
            {
                this.client = Client.Options.HttpClientFactory.GetHttpClient(timeout ?? DEFAULT_HTTP_CLIENT_TIMEOUT);
            }

            return this.client.GetHttpClient();
        }

        private async Task<string> SendAsync(IHttpClient client, string data, HttpMethod method, CancellationToken cancellationToken)
        {
            var responseData = string.Empty;
            var statusCode = HttpStatusCode.OK;
            var requestData = data;
            var url = string.Empty;

            try
            {
                url = await this.BuildUrlAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new FirebaseException("Couldn't build the url", requestData, responseData, statusCode, ex);
            }

            var message = new HttpRequestMessage(method, url, requestData);

            try
            {
                var result = await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
                statusCode = result.StatusCode;
                responseData = await result.Content.ReadAsStringAsync().ConfigureAwait(false);

                result.EnsureSuccessStatusCode();

                return responseData;
            }
            catch (Exception ex)
            {
                throw new FirebaseException(url, requestData, responseData, statusCode, ex);
            }
        }
    }
}

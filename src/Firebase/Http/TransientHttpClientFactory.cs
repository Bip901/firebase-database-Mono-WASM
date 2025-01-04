using System;
using Firebase.Database.Http;

namespace Firebase
{
    internal sealed class TransientHttpClientFactory : IHttpClientFactory
    {
        public IHttpClientProxy GetHttpClient(TimeSpan? timeout)
        {
            var client = HttpClientProvider.Constructor(timeout: timeout);

            return new SimpleHttpClientProxy(client);
        }
    }
}

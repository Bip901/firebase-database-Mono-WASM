using Firebase.Database.Http;

namespace Firebase
{
    internal sealed class SimpleHttpClientProxy : IHttpClientProxy
    {
        private readonly IHttpClient _httpClient;

        public SimpleHttpClientProxy(IHttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public IHttpClient GetHttpClient()
        {
            return _httpClient;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}

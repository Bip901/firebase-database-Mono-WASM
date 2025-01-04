using System;

namespace Firebase.Database.Http
{
    public static class HttpClientProvider
    {
        public delegate IHttpClient HttpClientConstructor(bool allowAutoRedirect = false, TimeSpan? timeout = null);

        public static HttpClientConstructor Constructor { get; set; }
    }
}

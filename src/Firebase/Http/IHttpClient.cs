using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Firebase.Database.Http
{
    public interface IHttpClient : IDisposable
    {
        Dictionary<string, string> DefaultRequestHeaders { get; }

        Task<HttpResponseMessage> GetAsync(string requestUri, CancellationToken cancellationToken = default);

        Task<HttpResponseMessage> DeleteAsync(string requestUri, CancellationToken cancellationToken = default);

        Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken cancellationToken = default);
    }
}

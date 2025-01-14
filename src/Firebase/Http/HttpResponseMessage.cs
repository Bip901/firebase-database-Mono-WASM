using System;
using System.Net;

namespace Firebase.Database.Http
{
    public class HttpResponseMessage : IDisposable
    {
        public HttpStatusCode StatusCode { get; }

        public HttpContent Content { get; }

        public string[] ResponseHeaders { get; }

        public HttpResponseMessage(HttpStatusCode statusCode, string[] responseHeaders, HttpContent content)
        {
            StatusCode = statusCode;
            ResponseHeaders = responseHeaders;
            Content = content;
        }

        /// <summary>
        /// Throws an exception if <see cref="StatusCode"/> was not in the range 200-299.
        /// </summary>
        /// <exception cref="HttpRequestException"/>
        /// <returns>this.</returns>
        public HttpResponseMessage EnsureSuccessStatusCode()
        {
            int statusCodeInt = (int)StatusCode;
            if (statusCodeInt < 200 || statusCodeInt > 299)
            {
                throw new HttpRequestException(statusCodeInt);
            }
            return this;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Content.Dispose();
        }
    }
}

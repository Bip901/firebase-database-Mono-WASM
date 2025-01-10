using System;

namespace Firebase.Database.Http
{
    /// <summary>
    /// An exception thrown when an HTTP response contains a non-success status code.
    /// </summary>
    public class HttpRequestException : Exception
    {
        /// <summary>
        /// The received status code.
        /// </summary>
        public int StatusCode { get; }

        /// <summary>
        /// Creates a new <see cref="HttpRequestException"/>.
        /// </summary>
        public HttpRequestException(int statusCode, Exception inner = null) : base($"Received non-success status code {statusCode}", inner)
        {
            StatusCode = statusCode;
        }
    }
}

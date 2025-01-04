namespace Firebase.Database.Http
{
    public class HttpRequestMessage
    {
        public HttpMethod Method { get; }
        public string Url { get; }
        public string Content { get; }

        public HttpRequestMessage(HttpMethod method, string url, string content = null)
        {
            Method = method;
            Url = url;
            Content = content;
        }
    }
}

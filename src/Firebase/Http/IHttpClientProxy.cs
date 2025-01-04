using System;
using Firebase.Database.Http;

namespace Firebase
{
    public interface IHttpClientProxy : IDisposable
    {
        IHttpClient GetHttpClient();
    }
}

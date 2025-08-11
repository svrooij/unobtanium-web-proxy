using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Unobtanium.Web.Proxy.Helpers;
internal static class NativeHttpMessagesHelper
{
    internal static HttpResponseMessage ConnectOkResponse( Version httpVersion )
    {
        var response = new HttpResponseMessage
        {
            Version = httpVersion,
            StatusCode = System.Net.HttpStatusCode.OK,
            ReasonPhrase = "Connection Established",
            Content = new ByteArrayContent([]) // Empty content for CONNECT response, hopefully with Content-Length header set to 0
        };
        return response;
    }

    internal static HttpResponseMessage Clone (this HttpResponseMessage original)
    {
        var resp = new HttpResponseMessage(original.StatusCode)
        {
            ReasonPhrase = original.ReasonPhrase,
            Content = original.Content
        };

        foreach(var header in original.Headers)
        {
            resp.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return resp;
    }
}

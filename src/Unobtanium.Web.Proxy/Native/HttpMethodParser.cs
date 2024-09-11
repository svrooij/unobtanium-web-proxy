using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unobtanium.Web.Proxy.Native;
internal static class HttpMethodParser
{
    internal static System.Net.Http.HttpMethod ParseMethodFromString(string method)
    {
        return method switch
        {
            "GET" => System.Net.Http.HttpMethod.Get,
            "POST" => System.Net.Http.HttpMethod.Post,
            "PUT" => System.Net.Http.HttpMethod.Put,
            "DELETE" => System.Net.Http.HttpMethod.Delete,
            "HEAD" => System.Net.Http.HttpMethod.Head,
            "OPTIONS" => System.Net.Http.HttpMethod.Options,
            "TRACE" => System.Net.Http.HttpMethod.Trace,
            "PATCH" => System.Net.Http.HttpMethod.Patch,
            _ => throw new ArgumentException("Invalid HTTP method", nameof(method))
        };
    }
}

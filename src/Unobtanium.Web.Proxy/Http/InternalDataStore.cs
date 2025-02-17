﻿using System.Collections.Generic;

namespace Unobtanium.Web.Proxy.Http;

internal class InternalDataStore : Dictionary<string, object>
{
    public bool TryGetValueAs<T> ( string key, out T? value )
    {
        var result = TryGetValue(key, out var value1);
        if (result)
            value = (T?)value1;
        else
            // hack: https://stackoverflow.com/questions/54593923/nullable-reference-types-with-generic-return-type
            value = default!;

        return result;
    }

    public T GetAs<T> ( string key )
    {
        return (T)this[key];
    }
}

﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Unobtanium.Web.Proxy.StreamExtended.Network;

namespace Unobtanium.Web.Proxy.Helpers;

internal class NullWriter : IHttpStreamWriter
{
    private NullWriter ()
    {
    }

    public static NullWriter Instance { get; } = new();

    public bool IsNetworkStream => false;

    public Task WriteAsync ( byte[] buffer, int offset, int count, CancellationToken cancellationToken )
    {
        return Task.CompletedTask;
    }

    public ValueTask WriteLineAsync ( CancellationToken cancellationToken = default )
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask WriteLineAsync ( string value, CancellationToken cancellationToken = default )
    {
        return ValueTask.CompletedTask;
    }
}

using System;

namespace Unobtanium.Web.Proxy.Http2;

[Flags]
internal enum Http2FrameFlag : byte
{
    Ack = 0x01,
    EndStream = 0x01, // Should this be different?, bug??
    EndHeaders = 0x04,
    Padded = 0x08,
    Priority = 0x20
}

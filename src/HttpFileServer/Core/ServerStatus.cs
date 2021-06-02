using System;
using System.Collections.Generic;
using System.Text;

namespace HttpFileServer.Core
{
    public enum ServerStatus
    {
        Ready,
        Error,
        Starting,
        Running,
        Stoping,
        Stopped
    }
}
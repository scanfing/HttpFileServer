using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace HttpFileServer.Handlers
{
    public interface IHttpHandler
    {
        #region Methods

        Task ProcessRequest(HttpListenerContext context);

        #endregion Methods
    }
}
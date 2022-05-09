using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace HttpFileServer.Handlers
{
    public abstract class HttpHandlerBase : IHttpHandler
    {
        #region Constructors

        public HttpHandlerBase(string rootDir)
        {
            SourceDir = rootDir;
        }

        #endregion Constructors

        #region Properties

        public bool EnableUpload { get; set; }

        public string SourceDir { get; private set; }

        #endregion Properties

        #region Methods

        public abstract Task ProcessRequest(HttpListenerContext context);

        #endregion Methods
    }
}
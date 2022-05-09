using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using HttpFileServer.Web;

namespace HttpFileServer.Handlers
{
    public class HttpPostHandler : HttpHandlerBase
    {
        #region Fields

        private HttpPostFileHandler _postFileHandler;

        #endregion Fields

        #region Constructors

        public HttpPostHandler(string rootDir) : base(rootDir)
        {
            _postFileHandler = new HttpPostFileHandler(rootDir);
        }

        #endregion Constructors

        #region Methods

        public override async Task ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            if (context.Request.HttpMethod.ToUpper() != "POST")
                return;

            if (request.ContentType.StartsWith("multipart/form-data;", StringComparison.OrdinalIgnoreCase))
                await _postFileHandler.ProcessRequest(context);
            else
                response.StatusCode = (int)HttpStatusCode.Forbidden;
        }

        #endregion Methods
    }
}
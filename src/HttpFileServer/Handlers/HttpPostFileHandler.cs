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
    public class HttpPostFileHandler
    {
        #region Fields

        private string _rootDir;

        #endregion Fields

        #region Constructors

        public HttpPostFileHandler(string rootDir)
        {
            _rootDir = rootDir;
        }

        #endregion Constructors

        #region Methods

        public async Task ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            if (request.ContentLength64 > int.MaxValue)
            {
                response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
                return;
            }
            var contents = await request.GetMultipartContent();
            foreach (var content in contents)
            {
                var postFile = content.GetAsPostedFile();
                var dstFile = Path.Combine(_rootDir, request.Url.LocalPath.TrimStart('/'), postFile.FileName);
                if (dstFile != _rootDir)
                    postFile.SaveAs(dstFile);
            }
            response.RedirectLocation = request.Url.AbsoluteUri;
            response.StatusCode = (int)HttpStatusCode.Redirect;
        }

        #endregion Methods
    }
}
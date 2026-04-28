using System.Net;
using System.Threading.Tasks;

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

            await _postFileHandler.ProcessRequest(context);
        }

        #endregion Methods
    }
}
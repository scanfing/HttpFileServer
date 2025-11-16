using System;
using System.IO;
using System.Web;
using System.Web.Script.Serialization;
using System.Text;
using System.Net;
using System.Threading.Tasks;

namespace HttpFileServer.Handlers
{
    public class FileInfoApiHandler : IHttpHandler
    {
        public async Task ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            response.ContentType = "application/json";
            string path = request.QueryString["path"];
            if (string.IsNullOrEmpty(path))
            {
                response.StatusCode = 400;
                await response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("{\"error\":\"Missing path parameter\"}"), 0, 38);
                return;
            }
            string absPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path.TrimStart('/', '\\'));
            if (!File.Exists(absPath))
            {
                response.StatusCode = 404;
                await response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("{\"error\":\"File not found\"}"), 0, 32);
                return;
            }
            var finfo = new FileInfo(absPath);
            var info = new
            {
                name = finfo.Name,
                size = finfo.Length,
                extension = finfo.Extension,
                modified = finfo.LastWriteTime,
                created = finfo.CreationTime,
                fullPath = finfo.FullName
            };
            var json = new JavaScriptSerializer().Serialize(info);
            var bytes = Encoding.UTF8.GetBytes(json);
            await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        }
    }
}
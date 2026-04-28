using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using HttpFileServer.Web;
using Newtonsoft.Json;

namespace HttpFileServer.Handlers
{
    public class HttpPostFileHandler : HttpHandlerBase
    {
        #region Constructors

        public HttpPostFileHandler(string rootDir) : base(rootDir) { }

        #endregion Constructors

        #region Methods

        public override async Task ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            // CORS preflight
            if (request.HttpMethod.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                response.AddHeader("Access-Control-Allow-Origin", "*");
                response.AddHeader("Access-Control-Allow-Methods", "POST,OPTIONS");
                response.AddHeader("Access-Control-Allow-Headers", "Content-Type,X-Requested-With");
                response.StatusCode = (int)HttpStatusCode.NoContent;
                return;
            }

            if (!request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                return;
            }

            if (request.ContentLength64 > int.MaxValue)
            {
                response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
                return;
            }

            if (string.IsNullOrEmpty(request.ContentType) || !request.ContentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            {
                response.StatusCode = (int)HttpStatusCode.UnsupportedMediaType;
                return;
            }

            // POST URL 即为目标文件路径（由前端将文件名拼接到请求路径中）
            var urlLocalPath = request.Url.LocalPath.TrimStart('/');
            var targetPath = Path.GetFullPath(Path.Combine(SourceDir, urlLocalPath.Replace('/', Path.DirectorySeparatorChar)));
            // 安全校验：目标路径必须在服务根目录内
            var fullSourceDir = Path.GetFullPath(SourceDir).TrimEnd(Path.DirectorySeparatorChar);
            if (!targetPath.StartsWith(fullSourceDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                response.StatusCode = (int)HttpStatusCode.Forbidden;
                return;
            }
            var targetFileName = Path.GetFileName(targetPath);
            if (string.IsNullOrEmpty(targetFileName))
            {
                var errJson = JsonConvert.SerializeObject(new { ok = false, error = "Request URL must include the target file name." });
                var errBuff = Encoding.UTF8.GetBytes(errJson);
                response.ContentType = "application/json"; response.ContentLength64 = errBuff.LongLength; await response.OutputStream.WriteAsync(errBuff, 0, errBuff.Length); response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }
            var targetDir = Path.GetDirectoryName(targetPath);
            try { if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir); } catch { }
            var results = new List<object>();
            try
            {
                var contents = await request.GetMultipartContent();
                // 每次只处理请求中的第一个文件，目标路径已由 URL 指定
                var fileContent = contents.FirstOrDefault(c => c.IsFile);
                if (fileContent == null)
                {
                    var errJson = JsonConvert.SerializeObject(new { ok = false, error = "No file found in request." });
                    var errBuff = Encoding.UTF8.GetBytes(errJson);
                    response.ContentType = "application/json"; response.ContentLength64 = errBuff.LongLength; await response.OutputStream.WriteAsync(errBuff, 0, errBuff.Length); response.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }
                var postFile = fileContent.GetAsPostedFile();
                if (postFile == null || string.IsNullOrWhiteSpace(postFile.FileName))
                {
                    var errJson = JsonConvert.SerializeObject(new { ok = false, error = "Invalid file in request." });
                    var errBuff = Encoding.UTF8.GetBytes(errJson);
                    response.ContentType = "application/json"; response.ContentLength64 = errBuff.LongLength; await response.OutputStream.WriteAsync(errBuff, 0, errBuff.Length); response.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }
                var dstFile = EnsureUniqueFile(targetPath);
                try
                {
                    postFile.SaveAs(dstFile);
                    results.Add(new { name = targetFileName, size = postFile.ContentLength, saved = true, finalPath = dstFile.Replace(SourceDir, ""), contentType = postFile.ContentType });
                }
                catch (Exception ex)
                {
                    results.Add(new { name = targetFileName, size = postFile.ContentLength, saved = false, error = ex.Message });
                }

                var json = JsonConvert.SerializeObject(new { ok = true, files = results });
                var buff = Encoding.UTF8.GetBytes(json);
                response.ContentType = "application/json"; response.ContentLength64 = buff.LongLength; await response.OutputStream.WriteAsync(buff, 0, buff.Length); response.StatusCode = (int)HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                var json = JsonConvert.SerializeObject(new { ok = false, error = ex.Message }); var buff = Encoding.UTF8.GetBytes(json); response.ContentType = "application/json"; response.ContentLength64 = buff.LongLength; await response.OutputStream.WriteAsync(buff, 0, buff.Length); response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
        }

        private string EnsureUniqueFile(string path)
        {
            if (!File.Exists(path)) return path;
            var dir = Path.GetDirectoryName(path);
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            int i =1;
            string candidate;
            do
            {
                candidate = Path.Combine(dir, $"{name}({i}){ext}");
                i++;
            } while (File.Exists(candidate));
            return candidate;
        }

        #endregion Methods
    }
}
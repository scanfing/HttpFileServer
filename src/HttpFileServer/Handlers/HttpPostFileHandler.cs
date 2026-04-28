using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
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

            // POST URL 即为目标文件路径（由前端将文件名拼接到请求路径中）
            var urlLocalPath = request.Url.LocalPath.TrimStart('/');
            var targetPath = Path.GetFullPath(Path.Combine(SourceDir, urlLocalPath.Replace('/', Path.DirectorySeparatorChar)));
            // 安全校验：目标路径必须在服务根目录内
            var fullSourceDir = Path.GetFullPath(SourceDir).TrimEnd(Path.DirectorySeparatorChar);
            if (!targetPath.Equals(fullSourceDir, StringComparison.OrdinalIgnoreCase) &&
                !targetPath.StartsWith(fullSourceDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
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

            // 将请求体直接流式写入目标文件，无内存缓冲，支持任意大小文件
            var dstFile = EnsureUniqueFile(targetPath);
            long savedSize = 0;
            try
            {
                using (var fs = new FileStream(dstFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    await request.InputStream.CopyToAsync(fs);
                    savedSize = fs.Length;
                }
                var json = JsonConvert.SerializeObject(new { ok = true, files = new[] { new { name = targetFileName, size = savedSize, saved = true, finalPath = dstFile.Replace(SourceDir, ""), contentType = request.ContentType } } });
                var buff = Encoding.UTF8.GetBytes(json);
                response.ContentType = "application/json"; response.ContentLength64 = buff.LongLength; await response.OutputStream.WriteAsync(buff, 0, buff.Length); response.StatusCode = (int)HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                // 删除写了一半的文件，避免留下损坏的文件
                try { if (File.Exists(dstFile)) File.Delete(dstFile); } catch { }
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
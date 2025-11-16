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

            var saveRoot = Path.Combine(SourceDir, request.Url.LocalPath.TrimStart('/'));
            try { if (!Directory.Exists(saveRoot)) Directory.CreateDirectory(saveRoot); } catch { }
            var results = new List<object>();
            try
            {
                var contents = await request.GetMultipartContent();
                //先收集所有普通字段(如 relativePath) 按 name 保存
                var formValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var part in contents.Where(c => c.IsFormItem))
                {
                    var val = part.GetAsString(Encoding.UTF8);
                    formValues[part.Name] = val;
                }
                // 对每个文件保存时如果存在 relativePath 字段则依据层级创建
                foreach (var content in contents.Where(c => c.IsFile))
                {
                    var postFile = content.GetAsPostedFile();
                    if (postFile == null || string.IsNullOrWhiteSpace(postFile.FileName)) continue;
                    var originalName = Path.GetFileName(postFile.FileName);
                    // 尝试从表单值中获取 relativePath，如果有则优先（可能包含目录+文件名）
                    var relativeKey = formValues.ContainsKey("relativePath") ? formValues["relativePath"] : originalName;
                    // 如果多个文件都共用一个 relativePath 会冲突，这里也尝试从文件名自身属性中获取
                    if (string.IsNullOrWhiteSpace(relativeKey)) relativeKey = originalName;
                    //规范化路径
                    // 移除上级路径标记并统一分隔符
                    relativeKey = relativeKey.Replace("../", "/").Replace("..\\", "/").Replace('\\','/').TrimStart('/');
                    // 提取目录部分
                    var dirPart = Path.GetDirectoryName(relativeKey.Replace('/', Path.DirectorySeparatorChar));
                    var targetDir = saveRoot;
                    if (!string.IsNullOrEmpty(dirPart))
                    {
                        targetDir = Path.Combine(saveRoot, dirPart);
                        try { Directory.CreateDirectory(targetDir); } catch { }
                    }
                    var dstFile = Path.Combine(targetDir, originalName);
                    dstFile = EnsureUniqueFile(dstFile);
                    try
                    {
                        postFile.SaveAs(dstFile);
                        results.Add(new { name = originalName, size = postFile.ContentLength, saved = true, relativePath = relativeKey, finalPath = dstFile.Replace(SourceDir, ""), contentType = postFile.ContentType });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new { name = originalName, size = postFile.ContentLength, saved = false, relativePath = relativeKey, error = ex.Message });
                    }
                }

                var json = JsonConvert.SerializeObject(new { ok = true, files = results });
                var buff = Encoding.UTF8.GetBytes(json);
                response.ContentType = "application/json"; response.ContentLength64 = buff.LongLength; await response.OutputStream.WriteAsync(buff,0,buff.Length); response.StatusCode = (int)HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                var json = JsonConvert.SerializeObject(new { ok=false, error=ex.Message }); var buff = Encoding.UTF8.GetBytes(json); response.ContentType="application/json"; response.ContentLength64=buff.LongLength; await response.OutputStream.WriteAsync(buff,0,buff.Length); response.StatusCode=(int)HttpStatusCode.InternalServerError;
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
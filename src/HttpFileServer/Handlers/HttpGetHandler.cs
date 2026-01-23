using HttpFileServer.Core;
using HttpFileServer.Services;
using HttpFileServer.Utils;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using HttpFileServer.Resources;

namespace HttpFileServer.Handlers
{
    public class HttpGetHandler : HttpHandlerBase
    {
        #region Fields

        private readonly string _debugResourceDir;
        private CacheService _cacheSrv;

        private CacheService _jsonCacheSrv;

        private JsonService _jsonSrv;

        #endregion Fields

        public bool EnableJson { get; private set; }

        #region Constructors

        public HttpGetHandler(string rootDir, CacheService cacheService, CacheService jsonCacheService, JsonService jsonService, bool enableUpload = false, bool enableJson = true, string debugResourceDir = null) : base(rootDir)
        {
            _cacheSrv = cacheService;
            _jsonCacheSrv = jsonCacheService;
            _jsonSrv = jsonService;
            EnableUpload = enableUpload;
            EnableJson = enableJson;
            _debugResourceDir = debugResourceDir;
        }

        #endregion Constructors

        #region Methods

        public override async Task ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            var url = request.Url.ToString();
            var localPath = request.Url.LocalPath;

            // If query indicates resource, delegate to ProcessResourceRequest
            var resourceQuery = request.QueryString.Get("type");
            if ("resource".Equals(resourceQuery, StringComparison.OrdinalIgnoreCase))
            {
                await ProcessResourceRequest(context);
                return;
            }

            // 增加特殊路径和空路径过滤，防止异常
            if (string.IsNullOrWhiteSpace(localPath))
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                response.Close();
                return;
            }

            var useJson = request.AcceptTypes != null && request.AcceptTypes.Any(p => p.Equals("application/json", StringComparison.OrdinalIgnoreCase));
            // JSON responses are always enabled when the client requests application/json
            if (useJson)
            {
                await ProcessJsonRequest(context);
                return;
            }

            var isPreview = url.Contains("preview=1");
            //预览时不触发下载/zip逻辑，直接正常响应内容
            if (isPreview)
            {
                var tmp = Path.Combine(SourceDir, request.Url.LocalPath.TrimStart('/'));
                var dstpath = tmp.Replace('/', '\\');
                await ResponseContentFull(dstpath, request, response, false, true);
                return;
            }

            var zipDownload = false;
            var downloadQuery = request.QueryString.Get("download");
            if ("1".Equals(downloadQuery) || "true".Equals(downloadQuery, StringComparison.OrdinalIgnoreCase))
            {
                zipDownload = true;
            }
            else
            {
                zipDownload = request.AcceptTypes != null && request.AcceptTypes.Any(p => p.Equals("application/zip", StringComparison.OrdinalIgnoreCase));
            }

            if (zipDownload)
            {
                await ProcessZipRequest(context);
                return;
            }

            var tmp2 = Path.Combine(SourceDir, request.Url.LocalPath.TrimStart('/'));
            var dstpath2 = tmp2.Replace('/', '\\');

            // If debug resource dir is provided and exists, bypass server-side cache for directory pages
            var isDir = Directory.Exists(dstpath2);
            var useDebugResources = !string.IsNullOrWhiteSpace(_debugResourceDir) && Directory.Exists(_debugResourceDir);
            if (isDir && useDebugResources)
            {
                // ensure clients don't cache the debug content
                response.AppendHeader("Cache-Control", "no-cache, no-store, must-revalidate");
                response.AppendHeader("Pragma", "no-cache");
                response.AppendHeader("Expires", "0");

                _cacheSrv.Delete(dstpath2);

                await ResponseContentFull(dstpath2, request, response);
                return;
            }

            //IfNoMatchCheck
            var requestETag = request.Headers["If-None-Match"];
            var cacheTag = _cacheSrv?.GetPathCacheId(dstpath2);

            if (requestETag == cacheTag)
            {
                response.StatusCode = (int)HttpStatusCode.NotModified;
            }
            else
            {
                response.AppendHeader("Cache-Control", "no-cache");
                if (string.IsNullOrEmpty(cacheTag))
                {
                    response.AppendHeader("Etag", cacheTag);
                }

                if (request.Headers.AllKeys.Count(p => p.ToLower() == "range") > 0)
                    await ResponseContentPartial(dstpath2, request, response);
                else
                    await ResponseContentFull(dstpath2, request, response);
            }
        }

        public virtual async Task ProcessResourceRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            var rel = request.Url.LocalPath.TrimStart('/');
            var useDebugResourceDirFlag = !string.IsNullOrWhiteSpace(_debugResourceDir) && Directory.Exists(_debugResourceDir);
            if (useDebugResourceDirFlag)
            {
                try
                {
                    var debugPath = Path.Combine(_debugResourceDir, rel.Replace('/', '\\'));
                    if (File.Exists(debugPath))
                    {
                        await ResponseContentFull(debugPath, request, response, false, true);
                        return;
                    }
                }
                catch { }
            }

            var filename = Path.GetFileName(rel ?? "");
            var ext = Path.GetExtension(filename);
            string content = null;
            byte[] contentBytes = null;
            string contentType = "application/octet-stream";

            try
            {
                // Try a set of candidate resource keys via ResourceManager. Prefer raw objects (byte[]) then strings.
                var candidates = new[] {
                    Path.GetFileNameWithoutExtension(filename),
                    filename,
                    Path.GetFileNameWithoutExtension(filename)?.Replace('.', '_')
                };

                foreach (var key in candidates.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        var obj = HtmlResource.ResourceManager.GetObject(key);
                        if (obj is byte[] b)
                        {
                            contentBytes = b;
                            break;
                        }
                        if (obj is System.IO.MemoryStream ms)
                        {
                            contentBytes = ms.ToArray();
                            break;
                        }

                        var str = HtmlResource.ResourceManager.GetString(key);
                        if (!string.IsNullOrWhiteSpace(str))
                        {
                            content = str;
                            break;
                        }
                    }
                    catch { }
                }
            }
            catch { }

            if (content == null && contentBytes == null)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            // Determine content type from filename extension when possible
            try
            {
                contentType = GetMimeType(filename ?? rel);
            }
            catch { }

            try
            {
                if (contentBytes != null)
                {
                    response.ContentType = contentType;
                    response.ContentLength64 = contentBytes.LongLength;
                    await response.OutputStream.WriteAsync(contentBytes, 0, contentBytes.Length);
                    await response.OutputStream.FlushAsync();
                    response.StatusCode = (int)HttpStatusCode.OK;
                }
                else
                {
                    var buff = Encoding.UTF8.GetBytes(content);
                    response.ContentEncoding = Encoding.UTF8;
                    response.ContentType = contentType;
                    response.ContentLength64 = buff.LongLength;
                    await response.OutputStream.WriteAsync(buff, 0, buff.Length);
                    await response.OutputStream.FlushAsync();
                    response.StatusCode = (int)HttpStatusCode.OK;
                }
            }
            catch (Exception)
            {
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="path"></param>
        /// <returns><see cref="Tuple$lt;string, Stream, bool&gt;"/> path,Stream,fileExist</returns>
        protected async Task<Tuple<string, Stream, bool>> GetResponseContentTypeAndStream(string path)
        {
            string contentType = "text/html";
            Stream stream = null;
            var fileExist = false;

            var data = _cacheSrv?.GetCache(path);
            if (data is null)
            {
                if (File.Exists(path))
                {
                    await FileAccessHelper.AddAccessCount(path);
                    fileExist = true;
                    stream = new FileStream(path, FileMode.Open, FileAccess.Read);
                    contentType = "application/octet-stream";
                }
                else if (Directory.Exists(path))
                {
                    var location = Path.GetFileName(path);
                    var title = "HttpFileServer";
                    if (path != SourceDir)
                    {
                        location = Path.GetFileName(SourceDir) + "\\" + path.Replace(SourceDir, "").Trim('\\');
                        title = Path.GetFileName(path.TrimEnd('\\')) + " -- HttpFileServer";
                    }
                    var content = HtmlExtension.GenerateHtmlContentForDir(SourceDir, path, path != SourceDir, EnableUpload, location, title, _debugResourceDir);
                    data = Encoding.UTF8.GetBytes(content);
                    _cacheSrv?.SaveCache(path, data);
                    stream = new MemoryStream(data);
                }
            }
            else
            {
                stream = new MemoryStream(data);
            }

            return new Tuple<string, Stream, bool>(contentType, stream, fileExist);
        }

        protected virtual async Task ProcessJsonRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            var tmp = Path.Combine(SourceDir, request.Url.LocalPath.TrimStart('/'));
            var dstpath = tmp.Replace('/', '\\');
            //IfNoMatchCheck
            var requestETag = request.Headers["If-None-Match"];
            var cacheTag = _jsonCacheSrv.GetPathCacheId(dstpath);

            if (requestETag == cacheTag)
            {
                response.StatusCode = (int)HttpStatusCode.NotModified;
            }
            else
            {
                response.AppendHeader("Cache-Control", "no-cache");
                response.AppendHeader("Etag", cacheTag);
                //文件
                if (File.Exists(dstpath))
                {
                    if (request.Headers.AllKeys.Count(p => p.ToLower() == "range") > 0)
                        await ResponseContentPartial(dstpath, request, response);
                    else
                        await ResponseContentFull(dstpath, request, response);
                    return;
                }

                //目录
                response.AddHeader("Content-Type", "application/json");

                var buff = _jsonCacheSrv.GetCache(dstpath);

                if (buff is null)
                {
                    DirInfoResponse respObj = new DirInfoResponse();
                    var dirinfo = new DirectoryInfo(dstpath);
                    respObj.LastWriteTime = dirinfo.LastWriteTime;
                    respObj.Name = dirinfo.Name;
                    respObj.RelativePath = Path.GetFullPath(dstpath).Replace(SourceDir, "/");

                    var dirs = Directory.GetDirectories(dstpath);
                    var files = Directory.GetFiles(dstpath);
                    var dlst = new List<DirPathInfo>();
                    var flst = new List<FilePathInfo>();
                    foreach (var p in dirs)
                    {
                        var dinfo = p.GetPathInfo(SourceDir) as DirPathInfo;
                        dlst.Add(dinfo);
                    }
                    foreach (var f in files)
                    {
                        var finfo = f.GetPathInfo(SourceDir) as FilePathInfo;
                        flst.Add(finfo);
                    }
                    respObj.Directories = dlst.ToArray();
                    respObj.Files = flst.ToArray();

                    var content = _jsonSrv.SerializeObject(respObj);
                    buff = Encoding.UTF8.GetBytes(content);
                    _jsonCacheSrv.SaveCache(dstpath, buff);
                }

                response.ContentLength64 = buff.LongLength;
                var stream = new MemoryStream(buff);
                await stream.CopyToAsync(response.OutputStream);
            }
        }

        protected async Task<bool> ProcessZipRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var resp = context.Response;

            var tmp = Path.Combine(SourceDir, request.Url.LocalPath.TrimStart('/'));
            var path = tmp.Replace('/', '\\');

            resp.ContentType = "application/zip";
            resp.ContentEncoding = Encoding.UTF8;

            // Determine a sensible default zip file name (dirname.zip)
            string dirname = null;
            try
            {
                if (Directory.Exists(path))
                {
                    // For directories use the folder name
                    dirname = new DirectoryInfo(path).Name;
                }
                else if (File.Exists(path))
                {
                    // For a single file use the file name without extension
                    dirname = Path.GetFileNameWithoutExtension(path);
                }
            }
            catch { }

            // Fallback to share root name when nothing derived
            if (string.IsNullOrWhiteSpace(dirname))
            {
                dirname = Path.GetFileName(SourceDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrWhiteSpace(dirname))
                    dirname = "download";
            }

            // Build Content-Disposition with both plain filename and RFC5987 encoded filename*
            var plainFileName = $"{dirname}.zip";
            var encodedFileName = Uri.EscapeDataString(plainFileName);
            var contentDisposition = $"attachment; filename=\"{plainFileName}\"; filename*=UTF-8''{encodedFileName}";
            try
            {
                // Use AppendHeader which works with HttpListenerResponse
                resp.AppendHeader("Content-Disposition", contentDisposition);
            }
            catch (Exception)
            {
                // best-effort only
            }

            using (var zipStream = new ZipOutputStream(resp.OutputStream))
            {
                zipStream.SetLevel(3); // 压缩等级0-9
                if (Directory.Exists(path))
                {
                    var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        var subFileName = file.Substring(path.Length).TrimStart('\\', '/');
                        var entry = new ZipEntry(subFileName)
                        {
                            DateTime = File.GetLastWriteTime(file)
                        };
                        zipStream.PutNextEntry(entry);
                        using (var fs = File.OpenRead(file))
                        {
                            await fs.CopyToAsync(zipStream);
                        }
                        zipStream.CloseEntry();
                    }
                }
                else if (File.Exists(path))
                {
                    var relfilename = Path.GetFileName(path);
                    var entry = new ZipEntry(relfilename)
                    {
                        DateTime = File.GetLastWriteTime(path)
                    };
                    zipStream.PutNextEntry(entry);
                    using (var fs = File.OpenRead(path))
                    {
                        await fs.CopyToAsync(zipStream);
                    }
                    zipStream.CloseEntry();
                }
                else
                {
                    resp.StatusCode = 404;
                    return false;
                }
                zipStream.Finish();
            }
            await resp.OutputStream.FlushAsync();
            resp.StatusCode = 200;
            System.Diagnostics.Trace.TraceInformation($"{DateTime.Now} {dirname}.zip Response Done.");
            return true;
        }

        /// <summary>
        /// 响应全数据
        /// </summary>
        /// <param name="path"></param>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="useRealMineType">响应头使用真实minetype</param>
        /// <returns></returns>
        protected async Task ResponseContentFull(string path, HttpListenerRequest request, HttpListenerResponse response, bool onlyHead = false, bool useRealMineType = false)
        {
            var tp = await GetResponseContentTypeAndStream(path);
            //预览时只设置Content-Type，不设置Content-Disposition
            if (useRealMineType)
            {
                response.ContentType = GetMimeType(path);
            }
            else
            {
                response.AddHeader("Content-Type", tp.Item1);
            }
            var stream = tp.Item2;
            if (stream is null)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }
            response.ContentLength64 = stream.Length;
            try
            {
                await stream.CopyToAsync(response.OutputStream);
                await response.OutputStream.FlushAsync();
                response.StatusCode = (int)HttpStatusCode.OK;
            }
            catch (Exception)
            {
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
            finally
            {
                if (tp.Item3)
                {
                    FileAccessHelper.SubAccessCount(path);
                }
                stream.Close();
            }
        }

        /// <summary>
        /// 断点续传响应支持
        /// </summary>
        /// <returns></returns>
        protected async Task ResponseContentPartial(string path, HttpListenerRequest request, HttpListenerResponse response, bool onlyHead = false)
        {
            var rangeStr = request.Headers["Range"];
            if (string.IsNullOrEmpty(rangeStr) || !rangeStr.StartsWith("bytes="))
            {
                response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                System.Diagnostics.Trace.TraceWarning($"Range header missing or invalid: '{rangeStr}'");
                return;
            }
            Tuple<long, long> range;
            try
            {
                range = GetRequestRange(rangeStr);
            }
            catch (Exception ex)
            {
                response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                System.Diagnostics.Trace.TraceError($"Range parse error: {rangeStr}, Exception: {ex}");
                return;
            }
            if (range.Item1 < 0)
            {
                response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                System.Diagnostics.Trace.TraceWarning($"Range start <0: {rangeStr}");
                return;
            }
            var tp = await GetResponseContentTypeAndStream(path);
            response.AddHeader("Content-Type", tp.Item1);
            var stream = tp.Item2;
            if (stream is null || !stream.CanRead)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                System.Diagnostics.Trace.TraceError($"File stream not available or not readable: {path}");
                return;
            }
            if (range.Item1 > stream.Length || range.Item2 > stream.Length || range.Item1 >= stream.Length)
            {
                response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                System.Diagnostics.Trace.TraceWarning($"Range out of bounds: {rangeStr}, FileLength: {stream.Length}");
                return;
            }
            response.AddHeader("Content-Range", $"bytes {range.Item1}-{range.Item2}/{stream.Length}");
            var buff = new byte[81920];
            var rangeEnd = range.Item2 > range.Item1 ? range.Item2 : stream.Length - 1;
            var bytesNeeds = rangeEnd - range.Item1 + 1;
            response.StatusCode = (int)HttpStatusCode.PartialContent;
            try
            {
                if (onlyHead)
                    return;
                stream.Seek(range.Item1, SeekOrigin.Begin);
                while (response.OutputStream.CanWrite && bytesNeeds > 0)
                {
                    var readcount = await stream.ReadAsync(buff, 0, (int)Math.Min(buff.Length, bytesNeeds));
                    if (readcount <= 0)
                        break;
                    await response.OutputStream.WriteAsync(buff, 0, readcount);
                    bytesNeeds -= readcount;
                    await Task.Delay(1);
                }
                if (response.OutputStream.CanWrite)
                    await response.OutputStream.FlushAsync();
            }
            catch (Exception ex)
            {
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                System.Diagnostics.Trace.TraceError($"PartialContent error: Path={path}, Range={rangeStr}, Exception={ex}");
            }
            finally
            {
                if (tp.Item3)
                {
                    FileAccessHelper.SubAccessCount(path);
                }
                stream.Close();
            }
        }

        private static string GetMimeType(string filePath)
        {
            // Prefer framework provided mapping which covers a wide set of common web file types.
            try
            {
                var mapped = System.Web.MimeMapping.GetMimeMapping(filePath);
                if (!string.IsNullOrWhiteSpace(mapped))
                    return mapped;
            }
            catch
            {
                // Ignore and fallback to built-in map below
            }

            var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
            switch (ext)
            {
                case ".txt": return "text/plain";
                case ".md": return "text/markdown";
                case ".log": return "text/plain";
                case ".csv": return "text/csv";
                case ".json": return "application/json";
                case ".xml": return "application/xml";
                case ".jpg": case ".jpeg": return "image/jpeg";
                case ".png": return "image/png";
                case ".gif": return "image/gif";
                case ".bmp": return "image/bmp";
                case ".webp": return "image/webp";
                case ".pdf": return "application/pdf";
                case ".html": case ".htm": return "text/html";
                // common web assets
                case ".css": return "text/css";
                case ".js": return "application/javascript";
                case ".map": return "application/json";
                case ".svg": return "image/svg+xml";
                case ".ico": return "image/x-icon";
                case ".woff": return "font/woff";
                case ".woff2": return "font/woff2";
                case ".ttf": return "font/ttf";
                case ".otf": return "font/otf";
                case ".eot": return "application/vnd.ms-fontobject";
                case ".wasm": return "application/wasm";
                case ".mp4": return "video/mp4";
                case ".webm": return "video/webm";
                case ".ogg": return "audio/ogg";
                case ".mp3": return "audio/mpeg";
                default: return "application/octet-stream";
            }
        }

        private Tuple<long, long> GetRequestRange(string rangeStr)
        {
            var rangeStart = 0L;
            var rangeEnd = 0L;
            var tmpstr = rangeStr.Replace("bytes=", "");
            var rgs = tmpstr.Split('-');

            if (!tmpstr.StartsWith("-"))
            {
                rangeStart = long.Parse(rgs[0]);
                if (!tmpstr.EndsWith("-"))
                {
                    rangeEnd = long.Parse(rgs[1]);
                }
            }
            else
            {
                if (!tmpstr.EndsWith("-"))
                {
                    rangeEnd = long.Parse(rgs[0]);
                }
            }

            return new Tuple<long, long>(rangeStart, rangeEnd);
        }

        #endregion Methods
    }
}
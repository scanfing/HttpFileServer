using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.Serialization;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HttpFileServer.Core;
using HttpFileServer.Handlers;
using HttpFileServer.Models;
using HttpFileServer.Services;
using HttpFileServer.Utils;

namespace HttpFileServer.Servers
{
    public class DefaultFileServer : IFileServer
    {
        #region Fields

        public long SingleCacheMaxSize = 81920;

        private CacheService _cacheSrv;

        private CancellationTokenSource _cts;

        /// <summary>
        /// 是否增加了防火墙放行端口，如果进行了增加，则停止服务时将其删除
        /// </summary>
        private bool _isFirewallOpened = false;

        private HttpListener _listener;

        private LocalFileService _localFileSrv;

        private HttpPostHandler _postHandler;

        #endregion Fields

        #region Constructors

        public DefaultFileServer(int port, string path, bool enableUpload = false)
        {
            Port = port;
            SourceDir = path.TrimEnd('\\').TrimEnd('/');
            EnableUpload = enableUpload;

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://+:{port}/");

            _localFileSrv = new LocalFileService(path);
            _localFileSrv.DirContentChanged += LocalFileSrv_DirContentChanged;
            _localFileSrv.PathDeleted += LocalFileSrv_PathDeleted;

            _cacheSrv = CacheService.GetDefault();
        }

        #endregion Constructors

        #region Events

        public event EventHandler<string> LogGenerated;

        public event EventHandler<RequestModel> NewReqeustIn;

        public event EventHandler<RequestModel> RequestOut;

        #endregion Events

        #region Properties

        public bool EnableUpload { get; }
        public int Port { get; }

        public string SourceDir { get; }

        #endregion Properties

        #region Methods

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _listener.Start();
            _localFileSrv.Start();
            _isFirewallOpened = FirewallHelper.NetFwAddPort("FileServer", Port, "TCP");
            _postHandler = new HttpPostHandler(SourceDir);

            _ = Task.Factory.StartNew(RunServerLoop);
            RecordLog($"Web Server[{Port} @ {SourceDir} ] Started.");
        }

        public void Stop()
        {
            _localFileSrv.Stop();
            _cts.Cancel();
            _listener.Stop();
            _cacheSrv.Clear();
            if (_isFirewallOpened)
                FirewallHelper.NetFwDelPort(Port, "TCP");
            RecordLog($"Web Server[{Port} @ {SourceDir} ] Stopped.");
        }

        private async void DoContext(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            var remotePoint = request.RemoteEndPoint.ToString();
            var method = request.HttpMethod.ToUpper();
            var url = Uri.UnescapeDataString(request.Url.PathAndQuery);
            var range = request.Headers["Range"];
            RecordLog($"{remotePoint} {method} {url} {range}");

            var requestModel = new RequestModel(url, request.RemoteEndPoint, method);
            NewReqeustIn?.Invoke(this, requestModel);

            try
            {
                switch (method)
                {
                    case "GET":
                        await DoGet(request, response);
                        break;

                    case "HEAD":
                        await DoHead(request, response);
                        break;

                    case "POST":
                        await _postHandler.ProcessRequest(context);
                        break;

                    default: response.StatusCode = (int)HttpStatusCode.Forbidden; break;
                }
            }
            catch (Exception ex)
            {
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                System.Diagnostics.Trace.TraceError(ex.Message);
            }
            finally
            {
                response.AddHeader("Access-Control-Allow-Origin", "*");
                response.AddHeader("Access-Control-Allow-Methods", "POST, GET, OPTIONS, DELETE");
                try { response.Close(); }
                catch { }
                RecordLog($"{remotePoint} {method} {url} {range} {response.StatusCode}");
                RequestOut?.Invoke(this, requestModel);
            }
        }

        private async Task DoGet(HttpListenerRequest request, HttpListenerResponse response)
        {
            var tmp = Path.Combine(SourceDir, request.Url.LocalPath.TrimStart('/'));
            var dstpath = tmp.Replace('/', '\\');
            //IfNoMatchCheck
            var requestETag = request.Headers["If-None-Match"];
            var cacheTag = _cacheSrv.GetPathCacheId(dstpath);

            if (requestETag == cacheTag)
            {
                response.StatusCode = (int)HttpStatusCode.NotModified;
            }
            else
            {
                response.AppendHeader("Cache-Control", "no-cache");
                response.AppendHeader("Etag", cacheTag);
                if (request.Headers.AllKeys.Count(p => p.ToLower() == "range") > 0)
                    await ResponseContentPartial(dstpath, request, response);
                else
                    await ResponseContentFull(dstpath, request, response);
            }
        }

        private async Task DoHead(HttpListenerRequest request, HttpListenerResponse response)
        {
            var tmp = Path.Combine(SourceDir, request.Url.LocalPath.TrimStart('/'));
            var dstpath = tmp.Replace('/', '\\');
            //IfNoMatchCheck
            var requestETag = request.Headers["If-None-Match"];
            var cacheTag = _cacheSrv.GetPathCacheId(dstpath);

            if (requestETag == cacheTag)
            {
                response.StatusCode = (int)HttpStatusCode.NotModified;
            }
            else
            {
                response.AppendHeader("Cache-Control", "no-cache");
                response.AppendHeader("Etag", cacheTag);
                if (request.Headers.AllKeys.Count(p => p.ToLower() == "range") > 0)
                    await ResponseContentPartial(dstpath, request, response, true);
                else
                    await ResponseContentFull(dstpath, request, response, true);
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

        /// <summary>
        /// </summary>
        /// <param name="path"></param>
        /// <returns><see cref="Tuple$lt;string, Stream, bool&gt;"/> path,Stream,fileExist</returns>
        private async Task<Tuple<string, Stream, bool>> GetResponseContentTypeAndStream(string path)
        {
            string contentType = "text/html";
            Stream stream = null;
            var fileExist = false;

            var data = _cacheSrv.GetCache(path);
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
                    var content = HtmlExtension.GenerateHtmlContentForDir(path, path != SourceDir, EnableUpload, location, title);
                    data = Encoding.UTF8.GetBytes(content);
                    _cacheSrv.SaveCache(path, data);
                    stream = new MemoryStream(data);
                }
            }
            else
            {
                stream = new MemoryStream(data);
            }

            return new Tuple<string, Stream, bool>(contentType, stream, fileExist);
        }

        private void LocalFileSrv_DirContentChanged(object sender, string e)
        {
            _cacheSrv.Delete(e);
        }

        private void LocalFileSrv_PathDeleted(object sender, string e)
        {
            _cacheSrv.Delete(e);
        }

        private void RecordLog(string content)
        {
            content = $"{DateTime.Now:yyy-MM-dd HH:mm:ss.fff} {content}";
            System.Diagnostics.Trace.WriteLine(content);
            LogGenerated?.BeginInvoke(this, content, null, null);
        }

        /// <summary>
        /// 响应全数据
        /// </summary>
        /// <param name="path"></param>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <returns></returns>
        private async Task ResponseContentFull(string path, HttpListenerRequest request, HttpListenerResponse response, bool onlyHead = false)
        {
            var tp = await GetResponseContentTypeAndStream(path);
            response.AddHeader("Content-Type", tp.Item1);
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
                //网络问题导致response.OutputStream无法写入
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
        private async Task ResponseContentPartial(string path, HttpListenerRequest request, HttpListenerResponse response, bool onlyHead = false)
        {
            var rangeStr = request.Headers["Range"];
            var range = GetRequestRange(rangeStr);

            if (range.Item1 < 0)
            {
                response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                return;
            }
            var tp = await GetResponseContentTypeAndStream(path);
            response.AddHeader("Content-Type", tp.Item1);
            var stream = tp.Item2;
            if (stream is null)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }
            if (range.Item1 > stream.Length || range.Item2 > stream.Length)
            {
                response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                return;
            }

            response.AddHeader("Content-Range", $"bytes {range.Item1}-{range.Item2}/{stream.Length}");

            var buff = new byte[81920];
            var rangeEnd = range.Item2 > range.Item1 ? range.Item2 : stream.Length - 1;
            var bytesNeeds = rangeEnd - range.Item1;
            response.StatusCode = (int)HttpStatusCode.PartialContent;
            try
            {
                if (onlyHead)
                    return;

                stream.Seek(range.Item1, SeekOrigin.Begin);
                while (bytesNeeds > 0)
                {
                    var readcount = stream.Read(buff, 0, 81920);
                    response.OutputStream.Write(buff, 0, (int)Math.Min(bytesNeeds, readcount));
                    bytesNeeds -= readcount;
                }

                await response.OutputStream.FlushAsync();
            }
            catch (Exception)
            {
                //网络问题导致response.OutputStream无法写入
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

        private void RunServerLoop()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var context = _listener.GetContext();
                    Task.Run(() =>
                    {
                        DoContext(context);
                    });
                }
                catch (Exception)
                {
                }
            }
        }

        #endregion Methods
    }
}
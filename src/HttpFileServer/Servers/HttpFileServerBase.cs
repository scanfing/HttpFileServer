using HttpFileServer.Core;
using HttpFileServer.Handlers;
using HttpFileServer.Models;
using HttpFileServer.Services;
using HttpFileServer.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HttpFileServer.Servers
{
    public abstract class HttpFileServerBase : IFileServer
    {
        #region Fields

        private CancellationTokenSource _cts;
        private HttpListener _listener;
        private LocalFileService _localFileSrv;

        private ConcurrentDictionary<string, IHttpHandler> RegisteredHandlers;

        #endregion Fields

        #region Constructors

        public HttpFileServerBase(int port, string path, bool enableUpload)
        {
            Port = port;
            SourceDir = path.TrimEnd('\\').TrimEnd('/');

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://+:{port}/");

            _localFileSrv = new LocalFileService(path);
            _localFileSrv.DirContentChanged += OnLocalFileSrv_DirContentChanged;
            _localFileSrv.PathDeleted += OnLocalFileSrv_PathDeleted;

            RegisteredHandlers = new ConcurrentDictionary<string, IHttpHandler>();
        }

        #endregion Constructors

        #region Events

        public event EventHandler<string> LogGenerated;

        public event EventHandler<RequestModel> NewReqeustIn;

        public event EventHandler<RequestModel> RequestOut;

        #endregion Events

        #region Properties

        public bool EnableUpload { get; protected set; }

        /// <summary>
        /// 是否增加了防火墙放行端口，如果进行了增加，则停止服务时将其删除
        /// </summary>
        public bool IsFirewallOpened { get; private set; } = false;

        public int Port { get; }

        public long SingleCacheMaxSize { get; protected set; } = 81920;

        public string SourceDir { get; }

        #endregion Properties

        #region Methods

        public virtual void Start()
        {
            _cts = new CancellationTokenSource();
            _listener.Start();
            _localFileSrv.Start();
            IsFirewallOpened = FirewallHelper.NetFwAddPort("FileServer", Port, "TCP");

            _ = Task.Factory.StartNew(RunServerLoop);
            RecordLog($"Web Server[{Port} @ {SourceDir} ] Started.");
        }

        public void Stop()
        {
            _localFileSrv.Stop();
            _cts.Cancel();
            _listener.Stop();

            if (IsFirewallOpened)
                FirewallHelper.NetFwDelPort(Port, "TCP");
            RecordLog($"Web Server[{Port} @ {SourceDir} ] Stopped.");
        }

        protected virtual IHttpHandler GetHandlerByMethodName(string method)
        {
            if (RegisteredHandlers.TryGetValue(method, out var handler))
                return handler;
            return null;
        }

        protected virtual void OnLocalFileSrv_DirContentChanged(object sender, string e)
        {
        }

        protected virtual void OnLocalFileSrv_PathDeleted(object sender, string e)
        {
        }

        protected virtual void RaiseRequestIn(RequestModel requestModel)
        {
            NewReqeustIn?.Invoke(this, requestModel);
        }

        protected virtual void RaiseRequestOut(RequestModel requestModel)
        {
            RequestOut?.Invoke(this, requestModel);
        }

        protected virtual void RecordLog(string content)
        {
            content = $"{DateTime.Now:yyy-MM-dd HH:mm:ss.fff} {content}";
            System.Diagnostics.Trace.WriteLine(content);
            LogGenerated?.BeginInvoke(this, content, null, null);
        }

        protected void RegisterHandler(string methodName, IHttpHandler handler)
        {
            RegisteredHandlers[methodName] = handler;
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
            RaiseRequestIn(requestModel);

            try
            {
                var handler = GetHandlerByMethodName(method);
                if (handler is null)
                    response.StatusCode = (int)HttpStatusCode.Forbidden;
                else
                    await handler.ProcessRequest(context);
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
                RaiseRequestOut(requestModel);
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
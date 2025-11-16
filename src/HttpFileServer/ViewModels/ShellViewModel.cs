using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Threading;
using HttpFileServer.Core;
using HttpFileServer.Infrastructure;
using HttpFileServer.Models;
using HttpFileServer.Servers;
using HttpFileServer.Services;
using HttpFileServer.Utils;

namespace HttpFileServer.ViewModels
{
    public class ShellViewModel : ViewModelBase
    {
        #region Fields

        private ConfigService _cfgSrv;
        private Config _config;
        private bool _enableUpload = false;
        private bool _isRunning = false;
        private ushort _listenPort =80;
        private string _logContent = string.Empty;
        private string _sourceDir;
        private ServerStatus _status = ServerStatus.Ready;

        #endregion Fields

        #region Constructors

        public ShellViewModel()
        {
            Title = "File Server";

            RequestModels = new ObservableCollection<RequestModel>();

            CommandStartServer = new CommandImpl(OnRequestStartServer, CanStartServer);
            CommandStopServer = new CommandImpl(OnRequestStopServer, CanStopServer);

            _cfgSrv = new ConfigService();
        }

        #endregion Constructors

        #region Properties

        public CommandImpl CommandStartServer { get; private set; }
        public CommandImpl CommandStopServer { get; private set; }
        public Dispatcher Dispatcher { get; set; }

        public bool EnableUpload
        {
            get => _enableUpload;
            set => SetProperty(ref _enableUpload, value);
        }

        public IFileServer FileServer { get; private set; }

        public bool IsRunning
        {
            get => _isRunning;
            set => SetProperty(ref _isRunning, value);
        }

        public ushort ListenPort
        {
            get => _listenPort;
            set => SetProperty(ref _listenPort, value);
        }

        public string LogContent
        {
            get => _logContent;
            private set => SetProperty(ref _logContent, value);
        }

        public ObservableCollection<RequestModel> RequestModels { get; private set; }

        public string SourceDir
        {
            get => _sourceDir;
            set => SetProperty(ref _sourceDir, value);
        }

        public ServerStatus Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        #endregion Properties

        #region Methods

        protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            base.OnPropertyChanged(propertyName);
            if (propertyName.Equals("SourceDir"))
            {
                CommandStartServer?.RaiseCanExecuteChanged();
            }
        }

        protected override void OnViewLoaded(object sender)
        {
            base.OnViewLoaded(sender);
            var cfg = _cfgSrv.GetConfig<Config>();

            if (cfg == null)
                cfg = new Config();

            _config = cfg;
            SourceDir = cfg.RootDir;
            ListenPort = cfg.Port;
            EnableUpload = cfg.EnableUpload;

            Dispatcher.ShutdownStarted += Dispatcher_ShutdownStarted;
        }

        protected override void OnViewUnLoaded(object sender)
        {
            if (IsRunning)
                OnRequestStopServer();

            _config.RootDir = SourceDir;
            _config.Port = (ushort)ListenPort;
            _config.EnableUpload = EnableUpload;
            _cfgSrv.SaveConfig(_config);

            base.OnViewUnLoaded(sender);
        }

        private bool CanStartServer()
        {
            return !IsRunning && !string.IsNullOrWhiteSpace(SourceDir);
        }

        private bool CanStopServer()
        {
            return IsRunning;
        }

        private void Dispatcher_ShutdownStarted(object sender, EventArgs e)
        {
            OnViewUnLoaded(sender);
        }

        private void FileServer_LogGenerated(object sender, string e)
        {
            LogContent += $"{e}{Environment.NewLine}";
        }

        private void FileServer_NewReqeustIn(object sender, RequestModel e)
        {
            Dispatcher?.InvokeAsync(() =>
            {
                RequestModels.Add(e);
            });
        }

        private void FileServer_RequestOut(object sender, RequestModel e)
        {
            Dispatcher?.InvokeAsync(() =>
            {
                RequestModels.Remove(e);
            });
        }

        private void OnRequestStartServer()
        {
            if (IsRunning)
                return;

            Directory.CreateDirectory(SourceDir);

            FileServer = new DefaultFileServer(ListenPort, SourceDir, true, EnableUpload); // 始终启用JSON

            FileServer.LogGenerated += FileServer_LogGenerated;
            FileServer.NewReqeustIn += FileServer_NewReqeustIn;
            FileServer.RequestOut += FileServer_RequestOut;

            Status = ServerStatus.Starting;
            FileServer.Start();
            var ips = IPHelper.GetAllLocalIP();
            foreach (var ip in ips)
            {
                LogContent += $"http://{ip}:{ListenPort}/{Environment.NewLine}";
            }
            IsRunning = true;
            Status = ServerStatus.Running;
            CommandStartServer?.RaiseCanExecuteChanged();
            CommandStopServer?.RaiseCanExecuteChanged();
        }

        private void OnRequestStopServer()
        {
            if (!IsRunning)
                return;

            Status = ServerStatus.Stoping;
            FileServer.Stop();
            FileServer.LogGenerated -= FileServer_LogGenerated;
            FileServer.NewReqeustIn -= FileServer_NewReqeustIn;
            FileServer.RequestOut -= FileServer_RequestOut;
            IsRunning = false;
            Status = ServerStatus.Stopped;
            CommandStartServer?.RaiseCanExecuteChanged();
            CommandStopServer?.RaiseCanExecuteChanged();
        }

        #endregion Methods
    }
}
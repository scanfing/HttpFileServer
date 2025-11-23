using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using HttpFileServer.Core;
using HttpFileServer.Infrastructure;
using HttpFileServer.Models;
using HttpFileServer.Servers;
using HttpFileServer.Services;
using HttpFileServer.Utils;
using HttpFileServer.Views;

namespace HttpFileServer.ViewModels
{
    public class ShellViewModel : ViewModelBase
    {
        #region Fields

        private ConfigService _cfgSrv;
        private Config _config;
        private bool _enableUpload = false;
        private bool _isRunning = false;
        private ushort _listenPort = 80;
        private string _logContent = string.Empty;
        private string _sourceDir;
        private ServerStatus _status = ServerStatus.Ready;
        private string _themeMode = "Light"; // Light / Dark / System

        #endregion Fields

        #region Constructors

        public ShellViewModel()
        {
            Title = "File Server";

            RequestModels = new ObservableCollection<RequestModel>();

            CommandStartServer = new CommandImpl(OnRequestStartServer, CanStartServer);
            CommandStopServer = new CommandImpl(OnRequestStopServer, CanStopServer);
            CommandToggleTheme = new CommandImpl(OnToggleTheme);

            _cfgSrv = new ConfigService();

            // 初始根据系统主题
            ThemeMode = DetectSystemTheme();
            ApplyThemeResources();
        }

        #endregion Constructors

        #region Properties

        private bool _autoStartOnLaunch = false;
        private bool _autoStartWithSystem = false;

        private bool _MinimizeToTrayAfterAutoStart = true;

        public bool AutoStartOnLaunch
        {
            get => _autoStartOnLaunch;
            set => SetProperty(ref _autoStartOnLaunch, value);
        }

        public bool AutoStartWithSystem
        {
            get => _autoStartWithSystem;
            set
            {
                if (SetProperty(ref _autoStartWithSystem, value))
                {
                    AutoStartHelper.SetAutoStart(value);
                }
            }
        }

        public CommandImpl CommandStartServer { get; private set; }

        public CommandImpl CommandStopServer { get; private set; }

        public CommandImpl CommandToggleTheme { get; private set; }

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

        public bool MinimizeToTrayAfterAutoStart
        {
            get => _MinimizeToTrayAfterAutoStart;
            set => SetProperty(ref _MinimizeToTrayAfterAutoStart, value);
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

        public string ThemeMode { get => _themeMode; set => SetProperty(ref _themeMode, value); }

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
            AutoStartOnLaunch = cfg.AutoStartOnLaunch;
            AutoStartWithSystem = cfg.AutoStartWithSystem;
            MinimizeToTrayAfterAutoStart = cfg.MinimizeToTrayAfterAutoStart;

            // 加载保存的主题模式
            if (!string.IsNullOrWhiteSpace(cfg.ThemeMode)) ThemeMode = cfg.ThemeMode; else ThemeMode = DetectSystemTheme();
            ApplyThemeResources();

            if (AutoStartHelper.IsProcessRunWithAutoStart() && _config.MinimizeToTrayAfterAutoStart)
            {
                var shell = sender as ShellView;
                shell.WindowState = WindowState.Minimized;
            }
            if ((AutoStartHelper.IsProcessRunWithAutoStart() && _config.AutoStartWithSystem) || _config.AutoStartOnLaunch)
            {
                OnRequestStartServer();
            }

            Dispatcher.ShutdownStarted += Dispatcher_ShutdownStarted;
        }

        protected override void OnViewUnLoaded(object sender)
        {
            if (IsRunning)
                OnRequestStopServer();

            _config.RootDir = SourceDir;
            _config.Port = (ushort)ListenPort;
            _config.EnableUpload = EnableUpload;
            _config.ThemeMode = ThemeMode; // 保存当前主题模式
            _config.AutoStartOnLaunch = AutoStartOnLaunch;
            _config.AutoStartWithSystem = AutoStartWithSystem;
            _config.MinimizeToTrayAfterAutoStart = MinimizeToTrayAfterAutoStart;

            _cfgSrv.SaveConfig(_config);

            base.OnViewUnLoaded(sender);
        }

        private void ApplyThemeResources()
        {
            var dict = GetWindowResources();
            string mode = ThemeMode == "System" ? DetectSystemTheme() : ThemeMode;
            if (mode == "Dark")
            {
                dict["WindowBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
                dict["WindowForegroundBrush"] = new SolidColorBrush(Colors.WhiteSmoke);
                dict["PanelBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));
                dict["PanelBorderBrush"] = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44));
                dict["AccentBrush"] = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
            }
            else // Light
            {
                dict["WindowBackgroundBrush"] = new SolidColorBrush(Colors.White);
                dict["WindowForegroundBrush"] = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11));
                dict["PanelBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
                dict["PanelBorderBrush"] = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD));
                dict["AccentBrush"] = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
            }
        }

        private bool CanStartServer()
        {
            return !IsRunning && !string.IsNullOrWhiteSpace(SourceDir);
        }

        private bool CanStopServer()
        {
            return IsRunning;
        }

        private string DetectSystemTheme()
        {
            // 简单使用系统窗口颜色亮度判断
            var col = SystemParameters.WindowGlassColor; // Win7+ Aero色
            var brightness = (col.R * 299 + col.G * 587 + col.B * 114) / 1000;
            return brightness < 128 ? "Dark" : "Light";
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

        private ResourceDictionary GetWindowResources()
        {
            foreach (Window w in Application.Current.Windows)
            {
                if (w.DataContext == this)
                    return w.Resources; // 当前窗口资源
            }
            return Application.Current.Resources; //退回应用级
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
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    LogContent += $"http://{ip}:{ListenPort}/{Environment.NewLine}";
                }
                else if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                {
                    LogContent += $"http://[{ip}]:{ListenPort}/{Environment.NewLine}";
                }
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

        private void OnToggleTheme()
        {
            if (ThemeMode == "Light") ThemeMode = "Dark";
            else if (ThemeMode == "Dark") ThemeMode = "System";
            else ThemeMode = "Light";
            ApplyThemeResources();
            // 实时保存配置
            _config.ThemeMode = ThemeMode;
            _cfgSrv.SaveConfig(_config);
        }

        #endregion Methods
    }
}
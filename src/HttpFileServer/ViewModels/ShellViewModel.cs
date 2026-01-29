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
using HttpFileServer;
using HttpFileServer.Infrastructure;
using HttpFileServer.Models;
using HttpFileServer.Servers;
using HttpFileServer.Services;
using HttpFileServer.Utils;
using HttpFileServer.Views;
using System.Linq;
using System.Net.NetworkInformation;
using System.Windows.Media.Imaging;
using QRCoder;

namespace HttpFileServer.ViewModels
{
    public class ShellViewModel : ViewModelBase
    {
        #region Fields

        private ConfigService _cfgSrv;
        private Config _config;
        private bool _enableUpload = false;
        private ImageSource _ipv4QrImage;
        private string _ipv4Text;
        private ImageSource _ipv6QrImage;
        private string _ipv6Text;
        private bool _isRunning = false;
        private ushort _listenPort = 80;
        private string _logContent = string.Empty;
        private bool _logIsReadOnly = false;
        private ObservableCollection<NetworkAdapterModel> _networkAdapters = new ObservableCollection<NetworkAdapterModel>();
        private NetworkAdapterModel _selectedNetworkAdapter;
        private int _selectedTabIndex = 0;
        private bool _shareTabEnabled = false;
        private string _sourceDir;
        private ServerStatus _status = ServerStatus.Ready;
        private string _themeMode = "Light";
        private bool _useWebServer = false;
        // Light / Dark / System

        #endregion Fields

        #region Constructors

        public ShellViewModel()
        {
            Title = "File Server";
            // capture app instance if available
            var _app = Application.Current as App;
            RequestModels = new ObservableCollection<RequestModel>();

            CommandStartServer = new CommandImpl(OnRequestStartServer, CanStartServer);
            CommandStopServer = new CommandImpl(OnRequestStopServer, CanStopServer);
            CommandToggleTheme = new CommandImpl(OnToggleTheme);

            // 默认：选中 配置 tab，分享禁用，日志在停止时可编辑
            SelectedTabIndex = 0;
            ShareTabEnabled = false;
            LogIsReadOnly = IsRunning; // false by default

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

        public ImageSource IPv4QrImage { get => _ipv4QrImage; private set => SetProperty(ref _ipv4QrImage, value); }

        public string IPv4Text { get => _ipv4Text; private set => SetProperty(ref _ipv4Text, value); }

        public ImageSource IPv6QrImage { get => _ipv6QrImage; private set => SetProperty(ref _ipv6QrImage, value); }

        public string IPv6Text { get => _ipv6Text; private set => SetProperty(ref _ipv6Text, value); }

        public bool IsRunning
        {
            get => _isRunning;
            set => SetProperty(ref _isRunning, value);
        }

        public ushort ListenPort
        {
            get => _listenPort;
            set
            {
                // Prevent changing the listen port while server is running
                if (IsRunning)
                    return;
                if (SetProperty(ref _listenPort, value))
                {
                    // Update share info in UI to reflect new port immediately
                    UpdateShareInfo();
                }
            }
        }

        public string LogContent
        {
            get => _logContent;
            private set => SetProperty(ref _logContent, value);
        }

        public bool LogIsReadOnly
        {
            get => _logIsReadOnly;
            set => SetProperty(ref _logIsReadOnly, value);
        }

        public bool MinimizeToTrayAfterAutoStart
        {
            get => _MinimizeToTrayAfterAutoStart;
            set => SetProperty(ref _MinimizeToTrayAfterAutoStart, value);
        }

        public ObservableCollection<NetworkAdapterModel> NetworkAdapters { get => _networkAdapters; }

        public ObservableCollection<RequestModel> RequestModels { get; private set; }

        public NetworkAdapterModel SelectedNetworkAdapter
        {
            get => _selectedNetworkAdapter;
            set
            {
                if (SetProperty(ref _selectedNetworkAdapter, value))
                {
                    UpdateShareInfo();
                }
            }
        }

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        public bool ShareTabEnabled
        {
            get => _shareTabEnabled;
            set => SetProperty(ref _shareTabEnabled, value);
        }

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

        public bool UseWebServer
        {
            get => _useWebServer;
            set => SetProperty(ref _useWebServer, value);
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
            UseWebServer = cfg.UseWebServer;
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

            // If application started with --debug-resource, show debug info in title
            string debugRes = null;
            try
            {
                var app = System.Windows.Application.Current as App;
                if (app != null)
                    debugRes = app.DebugResourcePath;
            }
            catch { }

            if (!string.IsNullOrWhiteSpace(debugRes))
            {
                Title = $"File Server - [ Html Debug ] -- {debugRes}";
                LogContent += ($"[ Html Debug ]{Environment.NewLine}{debugRes}{Environment.NewLine}{Environment.NewLine}");
            }

            if ((AutoStartHelper.IsProcessRunWithAutoStart() && _config.AutoStartWithSystem) || _config.AutoStartOnLaunch)
            {
                OnRequestStartServer();
            }

            Dispatcher.ShutdownStarted += Dispatcher_ShutdownStarted;

            // Populate network adapters for Share tab
            LoadNetworkAdapters();
        }

        protected override void OnViewUnLoaded(object sender)
        {
            if (IsRunning)
                OnRequestStopServer();

            _config.RootDir = SourceDir;
            _config.Port = (ushort)ListenPort;
            _config.EnableUpload = EnableUpload;
            _config.UseWebServer = UseWebServer;
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

        private ImageSource GenerateQrImage(string text)
        {
            try
            {
                using (var gen = new QRCodeGenerator())
                {
                    var data = gen.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
                    var pngQr = new PngByteQRCode(data);
                    var pngBytes = pngQr.GetGraphic(10);
                    using (var ms = new MemoryStream(pngBytes))
                    {
                        var img = new BitmapImage();
                        img.BeginInit();
                        img.CacheOption = BitmapCacheOption.OnLoad;
                        img.StreamSource = ms;
                        img.EndInit();
                        img.Freeze();
                        return img;
                    }
                }
            }
            catch
            {
                return null;
            }
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

        private void LoadNetworkAdapters()
        {
            try
            {
                NetworkAdapters.Clear();
                var nics = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback);
                foreach (var nic in nics)
                {
                    var props = nic.GetIPProperties();
                    var addrs = props.UnicastAddresses
                        .Where(u => u.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork || u.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                        .Select(u => u.Address).ToArray();
                    var model = new NetworkAdapterModel
                    {
                        Id = nic.Id,
                        Name = string.IsNullOrWhiteSpace(nic.Description) ? nic.Name : nic.Description,
                        IPv4 = addrs.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.ToString(),
                        IPv6 = addrs.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)?.ToString()
                    };
                    NetworkAdapters.Add(model);
                }
                SelectedNetworkAdapter = NetworkAdapters.FirstOrDefault();
            }
            catch { }
        }

        private void OnRequestStartServer()
        {
            if (IsRunning)
                return;

            Directory.CreateDirectory(SourceDir);

            // If application started with --debug-resource, prefer HtmlDebugServer
            string debugRes = null;
            try
            {
                var app = System.Windows.Application.Current as App;
                if (app != null)
                    debugRes = app.DebugResourcePath;
            }
            catch { }

            if (!string.IsNullOrWhiteSpace(debugRes))
            {
                // Pass debug resource directory to server base so handlers can read templates directly
                FileServer = new DefaultFileServer(ListenPort, SourceDir, true, EnableUpload)
                {
                };
            }
            else if (UseWebServer)
            {
                FileServer = new StaticWebHostServer(ListenPort, SourceDir, true, EnableUpload);
            }
            else
            {
                FileServer = new DefaultFileServer(ListenPort, SourceDir, true, EnableUpload); // 始终启用JSON
            }

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
                    var ipstr = ip.ToString();
                    if (ipstr.Contains("%"))
                        ipstr = ipstr.Substring(0, ipstr.IndexOf("%"));

                    LogContent += $"http://[{ipstr}]:{ListenPort}/{Environment.NewLine}";
                }
            }

            IsRunning = true;
            Status = ServerStatus.Running;
            // Ensure share info (QR and text) reflect the currently active port
            UpdateShareInfo();
            CommandStartServer?.RaiseCanExecuteChanged();
            CommandStopServer?.RaiseCanExecuteChanged();
            // Switch UI: show logs tab, enable share tab, make logs read-only
            SelectedTabIndex = 1; // 日志 tab
            ShareTabEnabled = true;
            LogIsReadOnly = true;
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
            // release reference to allow new server instance with different port
            FileServer = null;
            // Refresh share info to clear/reflect stopped state
            UpdateShareInfo();
            IsRunning = false;
            Status = ServerStatus.Stopped;
            CommandStartServer?.RaiseCanExecuteChanged();
            CommandStopServer?.RaiseCanExecuteChanged();
            // Switch UI: show config tab, disable share tab, make logs editable/readable
            SelectedTabIndex = 0; // 配置 tab
            ShareTabEnabled = false;
            LogIsReadOnly = false;
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

        private void UpdateShareInfo()
        {
            // Ensure updates happen on the UI thread
            var uiDispatcher = Dispatcher ?? Application.Current?.Dispatcher;
            Action work = () =>
            {
                if (SelectedNetworkAdapter == null)
                {
                    IPv4QrImage = null;
                    IPv6QrImage = null;
                    IPv4Text = string.Empty;
                    IPv6Text = string.Empty;
                    return;
                }

                // Build URLs
                if (!string.IsNullOrWhiteSpace(SelectedNetworkAdapter.IPv4))
                {
                    var url = $"http://{SelectedNetworkAdapter.IPv4}:{ListenPort}/";
                    IPv4Text = url;
                    IPv4QrImage = GenerateQrImage(url);
                }
                else
                {
                    IPv4Text = string.Empty;
                    IPv4QrImage = null;
                }

                if (!string.IsNullOrWhiteSpace(SelectedNetworkAdapter.IPv6))
                {
                    var ip = SelectedNetworkAdapter.IPv6;
                    if (ip.Contains("%")) ip = ip.Substring(0, ip.IndexOf('%'));
                    var url = $"http://[{ip}]:{ListenPort}/";
                    IPv6Text = url;
                    IPv6QrImage = GenerateQrImage(url);
                }
                else
                {
                    IPv6Text = string.Empty;
                    IPv6QrImage = null;
                }
            };

            if (uiDispatcher != null && !uiDispatcher.CheckAccess())
                uiDispatcher.BeginInvoke(work);
            else
                work();
        }

        #endregion Methods
    }
}
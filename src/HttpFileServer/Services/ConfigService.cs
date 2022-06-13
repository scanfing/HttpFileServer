using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using HttpFileServer.Core;

namespace HttpFileServer.Services
{
    public class ConfigService
    {
        #region Fields

        private string _cfgDir;
        private string _cfgFile;
        private JsonService _jsonSrv;

        #endregion Fields

        #region Constructors

        public ConfigService()
        {
            var name = GetType().Assembly.GetName().Name;
            var exe = Process.GetCurrentProcess().MainModule.FileName;
            _cfgDir = Path.GetDirectoryName(exe);
            _cfgFile = Path.Combine(_cfgDir, $"{name}.json");

            _jsonSrv = new JsonService();
        }

        #endregion Constructors

        #region Methods

        public T GetConfig<T>() where T : class
        {
            if (!File.Exists(_cfgFile))
                return default;

            var content = File.ReadAllText(_cfgFile);
            var _config = _jsonSrv.DeserializeObject<T>(content);
            return _config;
        }

        public bool SaveConfig<T>(T cfg)
        {
            try
            {
                var jsonSerializer = new JsonService();
                var content = _jsonSrv.SerializeObject(cfg);
                File.WriteAllText(_cfgFile, content);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion Methods
    }
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
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

        #endregion Fields

        #region Constructors

        public ConfigService()
        {
            var name = GetType().Assembly.GetName().Name;
            var exe = Process.GetCurrentProcess().MainModule.FileName;
            _cfgDir = Path.GetDirectoryName(exe);
            _cfgFile = Path.Combine(_cfgDir, $"{name}.json");
        }

        #endregion Constructors

        #region Methods

        public T GetConfig<T>() where T : class
        {
            if (!File.Exists(_cfgFile))
                return default;

            var content = File.ReadAllText(_cfgFile);
            var _config = JsonDeserialize<T>(content);
            return _config;
        }

        public bool SaveConfig<T>(T cfg)
        {
            try
            {
                DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer(typeof(T));
                var fs = new FileStream(_cfgFile, FileMode.Create, FileAccess.Write);
                jsonSerializer.WriteObject(fs, cfg);
                fs.Close();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static T JsonDeserialize<T>(string data)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            try
            {
                var buff = Encoding.UTF8.GetBytes(data);
                var memStream = new MemoryStream(buff);

                object obj = serializer.ReadObject(memStream);
                return (T)obj;
            }
            catch (Exception)
            {
                return default;
            }
        }

        #endregion Methods
    }
}
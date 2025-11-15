using System;
using System.Reflection;
using System.Windows;
using HttpFileServer.Resources;

namespace HttpFileServer
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        #region Constructors

        public App()
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
        }

        #endregion Constructors

        #region Methods

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var assname = new AssemblyName(args.Name).Name;
            if ("Newtonsoft.Json".Equals(assname))
            {
                var buff = DllResource.Newtonsoft_Json;
                return Assembly.Load(buff);
            }
            else if ("ICSharpCode.SharpZipLib".Equals(assname))
            {
                var buff = DllResource.ICSharpCode_SharpZipLib;
                return Assembly.Load(buff);
            }
            return null;
        }

        #endregion Methods
    }
}
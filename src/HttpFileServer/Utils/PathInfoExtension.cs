using HttpFileServer.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HttpFileServer.Utils
{
    public static class PathInfoExtension
    {
        #region Methods

        public static PathInfo GetPathInfo(this string path, string root)
        {
            root = root.TrimEnd('\\');
            if (File.Exists(path))
            {
                var finfo = new FileInfo(path);
                var pinfo = new FilePathInfo();
                pinfo.Name = finfo.Name;
                pinfo.RelativePath = finfo.FullName.Replace(root, "").Replace("\\", "/");
                pinfo.LastWriteTime = finfo.LastWriteTime;
                pinfo.Length = finfo.Length;
                pinfo.FileSize = SizeHelper.BytesToSize(pinfo.Length);
                return pinfo;
            }

            if (Directory.Exists(path))
            {
                var dinfo = new DirectoryInfo(path);
                var pinfo = new DirPathInfo();
                pinfo.Name = dinfo.Name;
                pinfo.RelativePath = dinfo.FullName.Replace(root, "").Replace("\\", "/");
                pinfo.LastWriteTime = dinfo.LastWriteTime;
                return pinfo;
            }

            return null;
        }

        #endregion Methods
    }
}
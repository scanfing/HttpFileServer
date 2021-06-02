using System;
using System.Collections.Generic;
using System.Text;

namespace HttpFileServer.Utils
{
    public static class SizeHelper
    {
        #region Fields

        public static readonly string[] SizeUnit = new string[] { "B", "KB", "MB", "GB", "TB", "PB", "EB" };

        #endregion Fields

        #region Methods

        public static string BytesToSize(long length)
        {
            if (length == 0)
                return "0B";
            var step = (int)Math.Truncate(Math.Log(length, 1024L));
            if (step > SizeUnit.Length)
                step = SizeUnit.Length - 1;
            var v = length / Math.Pow(1024L, step);
            v = Math.Round(v, 2);
            return $"{v}{SizeUnit[step]}";
        }

        #endregion Methods
    }
}
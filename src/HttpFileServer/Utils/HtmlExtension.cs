using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using HttpFileServer.Resources;

namespace HttpFileServer.Utils
{
    public static class HtmlExtension
    {
        #region Methods

        public static string GenerateHtmlContentForDir(string rootdir, string dstpath, bool showParent, bool enableUpload, string header, string title = "HttpFileServer")
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            var dirs = Directory.GetDirectories(dstpath);
            var files = Directory.GetFiles(dstpath);
            var index = 1;
            StringBuilder sb = new StringBuilder();
            if (showParent)
                sb.Append($"<tr {(index++ % 2 == 1 ? "class=\"pure-table-odd\"" : "")}><td><a class=\"icon up\" href=\"../\"><span id=\"parentDirText\">[上一级]</span></a></td><td></td><td></tr>");
            foreach (var dir in dirs)
            {
                var drinfo = new DirectoryInfo(dir);
                sb.AppendLine(drinfo.GetHtmlTableRowString(index++ % 2 == 1));
            }
            foreach (var file in files)
            {
                var finfo = new FileInfo(file);
                sb.AppendLine(finfo.GetHtmlTableRowString(index++ % 2 == 1));
            }
            stopWatch.Stop();
            var dtcache = DateTime.Now;
            var footerContent = $"<p style=\"text-align:center;color:#aac;\"><a href=\"https://github.com/scanfing/HttpFileServer\">HttpFileServer</a> 缓存时间: {dtcache:yyyy-MM-dd HH:mm:ss} 耗时:{stopWatch.ElapsedMilliseconds}ms</p>";

            //生成面包屑导航 header
            var sourceDir = rootdir.TrimEnd('\\', '/');
            var shareRoot = Path.GetFileName(sourceDir);
            var relPath = dstpath;
            if (relPath.StartsWith(sourceDir)) relPath = relPath.Substring(sourceDir.Length).TrimStart('\\', '/');
            var pathParts = relPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            var breadCrumb = new StringBuilder();
            breadCrumb.Append($"<a href='/'>" + shareRoot + "</a>");
            string curPath = "";
            for (int i = 0; i < pathParts.Length; i++)
            {
                curPath += "/" + pathParts[i];
                breadCrumb.Append($" / <a href='{curPath}/'>{pathParts[i]}</a>");
            }

            var content = HtmlResource.HtmlTemplate;
            content = content.Replace("{{title}}", title);
            content = content.Replace("{{header}}", breadCrumb.ToString());
            content = content.Replace("{{footerSectionContent}}", footerContent);
            content = content.Replace("{{uploadSection}}", enableUpload ? HtmlResource.UploadSection : "");
            content = content.Replace("{{tableRows}}", sb.ToString());
            return content;
        }

        public static string GetHtmlTableRowString(this FileSystemInfo info, bool isOdd)
        {
            if (info is DirectoryInfo dir)
                return $"<tr {(isOdd ? "class=\"pure-table-odd\"" : "")}><td><a class=\"icon dir\" href=\"./{dir.Name}/\">{dir.Name}</a></td><td class='tdsize'>--</td><td class='tdtime'>{dir.LastWriteTime:yyyy-MM-dd HH:mm:ss}</td></tr>";
            else if (info is FileInfo finfo)
                return $"<tr {(isOdd ? "class=\"pure-table-odd\"" : "")}><td><a class=\"icon file\" href=\"./{finfo.Name}\">{finfo.Name}</a></td><td class='tdsize'>{SizeHelper.BytesToSize(finfo.Length)}</td><td class='tdtime'>{finfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}</td></tr>";
            else
                return "";
        }

        #endregion Methods
    }
}
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
            {
                // 使用目录行模板渲染“上一级”按钮，结构与目录一致
                var parentRow = HtmlResource.TableRowDirTemplate
                    .Replace("./${file.name}/", "../")
                    .Replace("${file.name}", "上一级")
                    .Replace("${file.size}", "--")
                    .Replace("${file.modified}", "--")
                    .Replace("${file.type}", "目录");

                sb.AppendLine(parentRow);
                index++;
            }
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
            content = content.Replace("{{itemcount}}", (dirs.Length + files.Length).ToString());
            content = content.Replace("{{footer}}", footerContent);
            content = content.Replace("{{uploadSection}}", enableUpload ? HtmlResource.UploadSection : "");
            content = content.Replace("{{tableRows}}", sb.ToString());
            return content;
        }

        public static string GetHtmlTableRowString(this FileSystemInfo info, bool isOdd)
        {
            if (info is DirectoryInfo dir)
            {
                var dirStr = HtmlResource.TableRowDirTemplate;
                dirStr = dirStr.Replace("${file.name}", dir.Name);
                dirStr = dirStr.Replace("${file.size}", "--");
                dirStr = dirStr.Replace("${file.modified}", dir.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
                dirStr = dirStr.Replace("${file.type}", "--");
                return dirStr;
            }
            else if (info is FileInfo finfo)
            {
                var filestr = HtmlResource.TableRowFileTemplate;
                filestr = filestr.Replace("${file.name}", finfo.Name);
                filestr = filestr.Replace("${file.size}", SizeHelper.BytesToSize(finfo.Length));
                filestr = filestr.Replace("${file.modified}", finfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
                filestr = filestr.Replace("${file.type}", finfo.Extension);
                return filestr;
            }

            return "";
        }

        #endregion Methods
    }
}
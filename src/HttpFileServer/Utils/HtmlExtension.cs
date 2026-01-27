using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Text.RegularExpressions;
using HttpFileServer.Resources;

namespace HttpFileServer.Utils
{
    public static class HtmlExtension
    {
        #region Methods

        public static string GenerateHtmlContentForDir(string rootdir, string dstpath, bool showParent, bool enableUpload, string header, string title = "HttpFileServer", string debugResourceDir = null)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            var dirs = Directory.GetDirectories(dstpath);
            var files = Directory.GetFiles(dstpath);
            var index = 1;
            StringBuilder sb = new StringBuilder();

            // Load row templates - prefer debugResourceDir overrides
            string dirRowTemplate = HtmlResource.TableRowDirTemplate;
            string fileRowTemplate = HtmlResource.TableRowFileTemplate;
            try
            {
                if (!string.IsNullOrWhiteSpace(debugResourceDir))
                {
                    var dirTplPath = Path.Combine(debugResourceDir, "TableRowDirTemplate.html");
                    var fileTplPath = Path.Combine(debugResourceDir, "TableRowFileTemplate.html");
                    if (File.Exists(dirTplPath)) dirRowTemplate = File.ReadAllText(dirTplPath, Encoding.UTF8);
                    if (File.Exists(fileTplPath)) fileRowTemplate = File.ReadAllText(fileTplPath, Encoding.UTF8);
                }
            }
            catch { }

            if (showParent)
            {
                // 上一级目录行使用统一的样式和暗色主题高亮支持
                sb.AppendLine("<tr class=\"row-hover transition-colors\">" +
                              "<td class=\"px-6 py-4 whitespace-nowrap\"><div class=\"flex items-center\"><a href=\"../\" class=\"file-link\" data-filetype=\"up\"><i class=\"icon up mr-1\"></i><span class=\"file-name text-sm\">..</span></a></div></td>" +
                              "<td class=\"px-6 py-4 whitespace-nowrap text-sm text-gray-500\">--</td>" +
                              "<td class=\"px-6 py-4 whitespace-nowrap text-sm text-gray-500\">--</td>" +
                              "<td class=\"px-6 py-4 whitespace-nowrap text-sm text-gray-500\">上一级</td>" +
                              "<td class=\"px-6 py-4 whitespace-nowrap text-sm font-medium\">" +
                              "<button class=\"text-blue-400 hover:text-blue-900 mr-3\"><a href=\"../\">上一级</a></button>" +
                              "</td></tr>");
                index++;
            }

            foreach (var dir in dirs)
            {
                var di = new DirectoryInfo(dir);
                var row = dirRowTemplate;
                row = row.Replace("${file.name}", HttpUtility.HtmlEncode(di.Name));
                row = row.Replace("${file.size}", "--");
                row = row.Replace("${file.modified}", di.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
                row = row.Replace("${file.type}", "--");
                sb.AppendLine(row);
                index++;
            }
            foreach (var file in files)
            {
                var fi = new FileInfo(file);
                var row = fileRowTemplate;
                row = row.Replace("${file.name}", HttpUtility.HtmlEncode(fi.Name));
                row = row.Replace("${file.size}", SizeHelper.BytesToSize(fi.Length));
                row = row.Replace("${file.modified}", fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
                row = row.Replace("${file.type}", fi.Extension);
                sb.AppendLine(row);
                index++;
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

            // Try to load template and auxiliary resources from debugResourceDir when provided.
            string content;
            try
            {
                var htmlTemplatePath = string.IsNullOrWhiteSpace(debugResourceDir) ? null : Path.Combine(debugResourceDir, "HtmlTemplate.html");
                if (!string.IsNullOrWhiteSpace(htmlTemplatePath) && File.Exists(htmlTemplatePath))
                {
                    content = File.ReadAllText(htmlTemplatePath, Encoding.UTF8);
                }
                else
                {
                    content = HtmlResource.HtmlTemplate;
                }
            }
            catch
            {
                content = HtmlResource.HtmlTemplate;
            }
            content = content.Replace("{{title}}", title);
            content = content.Replace("{{header}}", breadCrumb.ToString());
            content = content.Replace("{{itemcount}}", (dirs.Length + files.Length).ToString());
            content = content.Replace("{{footer}}", footerContent);
            // Upload section: allow overriding from debug resource dir
            string uploadSection = HtmlResource.UploadSection;
            try
            {
                var uploadPath = string.IsNullOrWhiteSpace(debugResourceDir) ? null : Path.Combine(debugResourceDir, "UploadSection.html");
                if (!string.IsNullOrWhiteSpace(uploadPath) && File.Exists(uploadPath))
                    uploadSection = File.ReadAllText(uploadPath, Encoding.UTF8);
            }
            catch { }
            content = content.Replace("{{uploadSection}}", enableUpload ? uploadSection : "");
            content = content.Replace("{{tableRows}}", sb.ToString());

            // Final pass: replace any remaining {{key}} placeholders from known values (case-insensitive).
            try
            {
                var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["title"] = title,
                    ["header"] = breadCrumb.ToString(),
                    ["itemcount"] = (dirs.Length + files.Length).ToString(),
                    ["footer"] = footerContent,
                    ["uploadSection"] = enableUpload ? uploadSection : "",
                    ["tableRows"] = sb.ToString(),
                };

                content = Regex.Replace(content, "\\{\\{\\s*(.*?)\\s*\\}\\}", m =>
                {
                    var key = m.Groups[1].Value.Trim();
                    if (replacements.TryGetValue(key, out var val))
                        return val;
                    return m.Value; // leave unchanged if unknown
                }, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            }
            catch { }

            return content;
        }

        #endregion Methods
    }
}
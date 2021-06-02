using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;

namespace HttpFileServer.Web
{
    public static class HttpListenerRequestExtensions
    {
        #region Methods

        internal static byte[] GetMultipartBoundary(this HttpListenerRequest request)
        {
            // extract boundary value
            String b = request.GetAttributeFromHeader(request.ContentType, "boundary");
            if (b == null)
                return null;

            // prepend with "--" and convert to byte array
            b = "--" + b;
            return Encoding.ASCII.GetBytes(b.ToCharArray());
        }

        internal static async Task<MultipartContentElement[]> GetMultipartContent(this HttpListenerRequest request)
        {
            //// already parsed
            //if (_multipartContentElements != null)
            //    return _multipartContentElements;

            // check the boundary
            byte[] boundary = request.GetMultipartBoundary();
            if (boundary == null)
                return new MultipartContentElement[0];

            // read the content if not read already
            HttpRawUploadedContent content = await request.GetEntireRawContent();
            if (content == null)
                return new MultipartContentElement[0];

            // do the parsing
            var _multipartContentElements = HttpMultipartContentTemplateParser.Parse(content, content.Length, boundary, Encoding.UTF8);
            return _multipartContentElements;
        }

        private static String GetAttributeFromHeader(this HttpListenerRequest request, String headerValue, String attrName)
        {
            if (headerValue == null)
                return null;

            int l = headerValue.Length;
            int k = attrName.Length;

            // find properly separated attribute name
            int i = 1; // start searching from 1

            while (i < l)
            {
                i = CultureInfo.InvariantCulture.CompareInfo.IndexOf(headerValue, attrName, i, CompareOptions.IgnoreCase);
                if (i < 0)
                    break;
                if (i + k >= l)
                    break;

                char chPrev = headerValue[i - 1];
                char chNext = headerValue[i + k];
                if ((chPrev == ';' || chPrev == ',' || Char.IsWhiteSpace(chPrev)) && (chNext == '=' || Char.IsWhiteSpace(chNext)))
                    break;

                i += k;
            }

            if (i < 0 || i >= l)
                return null;

            // skip to '=' and the following whitespaces
            i += k;
            while (i < l && Char.IsWhiteSpace(headerValue[i]))
                i++;
            if (i >= l || headerValue[i] != '=')
                return null;
            i++;
            while (i < l && Char.IsWhiteSpace(headerValue[i]))
                i++;
            if (i >= l)
                return null;

            // parse the value
            String attrValue = null;

            int j;

            if (i < l && headerValue[i] == '"')
            {
                if (i == l - 1)
                    return null;
                j = headerValue.IndexOf('"', i + 1);
                if (j < 0 || j == i + 1)
                    return null;

                attrValue = headerValue.Substring(i + 1, j - i - 1).Trim();
            }
            else
            {
                for (j = i; j < l; j++)
                {
                    if (headerValue[j] == ' ' || headerValue[j] == ',')
                        break;
                    if (headerValue[j] == ';')
                        break;
                }

                if (j == i)
                    return null;

                attrValue = headerValue.Substring(i, j - i).Trim();
            }

            return attrValue;
        }

        // Reading posted content ...

        /*
         * Get attribute off header value
         */
        /*
     * Read entire raw content as byte array
     */

        private static async Task<HttpRawUploadedContent> GetEntireRawContent(this HttpListenerRequest request)
        {
            // threshold to go to file
            int fileThreshold = int.MaxValue;

            if (request.ContentLength64 > fileThreshold)
                throw new Exception("raw content too large.");
            // read the preloaded content

            HttpRawUploadedContent rawContent = new HttpRawUploadedContent(fileThreshold, (int)request.ContentLength64);
            var memstream = new MemoryStream();
            await request.InputStream.CopyToAsync(memstream);

            if (memstream.Length > 0)
            {
                rawContent.AddBytes(memstream.ToArray(), 0, (int)memstream.Length);
            }

            // read the remaing content

            //if (!_wr.IsEntireEntityBodyIsPreloaded())
            //{
            //    int remainingBytes = (request.ContentLength64 > 0) ? (int)(request.ContentLength64 - rawContent.Length) : Int32.MaxValue;

            // byte[] buf = new byte[8 * 1024]; int numBytesRead = rawContent.Length;

            // while (remainingBytes > 0) { int bytesToRead = buf.Length; if (bytesToRead >
            // remainingBytes) bytesToRead = remainingBytes;

            // int bytesRead = _wr.ReadEntityBody(buf, bytesToRead); if (bytesRead <= 0) break;

            // rawContent.AddBytes(buf, 0, bytesRead);

            //        remainingBytes -= bytesRead;
            //        numBytesRead += bytesRead;
            //    }
            //}

            rawContent.DoneAddingBytes();

            return rawContent;
        }

        #endregion Methods
    }
}
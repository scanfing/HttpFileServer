//------------------------------------------------------------------------------
// <copyright file="MultipartContentParser.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

/*
 * Multipart content parser.
 *
 * Copyright (c) 1998 Microsoft Corporation
 */

namespace HttpFileServer.Web
{
    using System.Text;

    using System.Collections;
    using System.Globalization;
    using System.Web.Util;
    using System;

    /*
     * Element of the multipart content
     */

    internal sealed class HttpMultipartContentTemplateParser
    {
        #region Fields

        // part separator
        private byte[] _boundary;

        private HttpRawUploadedContent _data;
        private ArrayList _elements = new ArrayList();

        // encoding
        private Encoding _encoding;

        // last boundary has extra --
        private bool _lastBoundaryFound;

        private int _length;

        private int _lineLength = -1;

        // currently parsed line
        private int _lineStart = -1;

        private String _partContentType;
        private int _partDataLength = -1;

        // current part's content data
        private int _partDataStart = -1;

        private String _partFilename;

        // current header values
        private String _partName;

        private int _pos;

        #endregion Fields

        #region Constructors

        private HttpMultipartContentTemplateParser(HttpRawUploadedContent data, int length, byte[] boundary, Encoding encoding)
        {
            _data = data;
            _length = length;
            _boundary = boundary;
            _encoding = encoding;
        }

        #endregion Constructors

        #region Methods

        internal static MultipartContentElement[] Parse(HttpRawUploadedContent data, int length, byte[] boundary, Encoding encoding)
        {
            HttpMultipartContentTemplateParser parser = new HttpMultipartContentTemplateParser(data, length, boundary, encoding);
            parser.ParseIntoElementList();
            return (MultipartContentElement[])parser._elements.ToArray(typeof(MultipartContentElement));
        }

        private bool AtBoundaryLine()
        {
            // check for either regular or last boundary line length

            int len = _boundary.Length;

            if (_lineLength != len && _lineLength != len + 2)
                return false;

            // match with boundary

            for (int i = 0; i < len; i++)
            {
                if (_data[_lineStart + i] != _boundary[i])
                    return false;
            }

            // regular boundary line?

            if (_lineLength == len)
                return true;

            // last boundary line? (has to end with "--")

            if (_data[_lineStart + len] != 45 || _data[_lineStart + len + 1] != 45)
                return false;

            _lastBoundaryFound = true; // remember that it is last
            return true;
        }

        private bool AtEndOfData()
        {
            return (_pos >= _length || _lastBoundaryFound);
        }

        private String ExtractValueFromContentDispositionHeader(String l, int pos, String name)
        {
            String pattern = " " + name + "=";
            int i1 = CultureInfo.InvariantCulture.CompareInfo.IndexOf(l, pattern, pos, CompareOptions.IgnoreCase);
            if (i1 < 0)
            {
                pattern = ";" + name + "=";
                i1 = CultureInfo.InvariantCulture.CompareInfo.IndexOf(l, pattern, pos, CompareOptions.IgnoreCase);
                if (i1 < 0)
                {
                    pattern = name + "=";
                    i1 = CultureInfo.InvariantCulture.CompareInfo.IndexOf(l, pattern, pos, CompareOptions.IgnoreCase);
                }
            }
            if (i1 < 0)
                return null;
            i1 += pattern.Length;
            if (i1 >= l.Length)
                return String.Empty;

            if (l[i1] == '"')
            {
                i1 += 1;
                int i2 = l.IndexOf('"', i1);
                if (i2 < 0)
                    return null;
                if (i2 == i1)
                    return String.Empty;

                return l.Substring(i1, i2 - i1);
            }
            else
            {
                int i2 = l.IndexOf(';', i1);
                if (i2 < 0)
                    i2 = l.Length;

                return l.Substring(i1, i2 - i1).Trim();
            }
        }

        private bool GetNextLine()
        {
            int i = _pos;

            _lineStart = -1;

            while (i < _length)
            {
                if (_data[i] == 10)
                { // '\n'
                    _lineStart = _pos;
                    _lineLength = i - _pos;
                    _pos = i + 1;

                    // ignore \r
                    if (_lineLength > 0 && _data[i - 1] == 13)
                        _lineLength--;

                    // line found
                    break;
                }

                if (++i == _length)
                {
                    // last line doesn't end with \n
                    _lineStart = _pos;
                    _lineLength = i - _pos;
                    _pos = _length;
                }
            }

            return (_lineStart >= 0);
        }

        private void ParseIntoElementList()
        {
            // Skip until first boundary

            while (GetNextLine())
            {
                if (AtBoundaryLine())
                    break;
            }

            if (AtEndOfData())
                return;

            // Parse the parts

            do
            {
                // Parse current part's headers

                ParsePartHeaders();

                if (AtEndOfData())
                    break;          // cannot stop after headers

                // Parse current part's data

                ParsePartData();

                if (_partDataLength == -1)
                    break;          // ending boundary not found

                // Remember the current part (if named)

                if (_partName != null)
                {
                    _elements.Add(new MultipartContentElement(
                                                             _partName,
                                                             _partFilename,
                                                             _partContentType,
                                                             _data,
                                                             _partDataStart,
                                                             _partDataLength));
                }
            }
            while (!AtEndOfData());
        }

        private void ParsePartData()
        {
            _partDataStart = _pos;
            _partDataLength = -1;

            while (GetNextLine())
            {
                if (AtBoundaryLine())
                {
                    // calc length: adjust to exclude [\r]\n before the separator
                    int iEnd = _lineStart - 1;
                    if (_data[iEnd] == 10)   // \n
                        iEnd--;
                    if (_data[iEnd] == 13)   // \r
                        iEnd--;

                    _partDataLength = iEnd - _partDataStart + 1;
                    break;
                }
            }
        }

        private void ParsePartHeaders()
        {
            _partName = null;
            _partFilename = null;
            _partContentType = null;

            while (GetNextLine())
            {
                if (_lineLength == 0)
                    break;  // empty line signals end of headers

                // get line as String
                byte[] lineBytes = new byte[_lineLength];
                _data.CopyBytes(_lineStart, lineBytes, 0, _lineLength);
                String line = _encoding.GetString(lineBytes);

                // parse into header and value
                int ic = line.IndexOf(':');
                if (ic < 0)
                    continue;   // not a header

                // remeber header
                String header = line.Substring(0, ic);

                if ("Content-Disposition".Equals(header, StringComparison.OrdinalIgnoreCase))
                {
                    System.Diagnostics.Trace.WriteLine(line);
                    // parse name and filename
                    _partName = ExtractValueFromContentDispositionHeader(line, ic + 1, "name");
                    _partFilename = ExtractValueFromContentDispositionHeader(line, ic + 1, "filename");
                }
                else if ("Content-Type".Equals(header, StringComparison.OrdinalIgnoreCase))
                {
                    System.Diagnostics.Trace.WriteLine(line);
                    _partContentType = line.Substring(ic + 1).Trim();
                }
            }
        }

        #endregion Methods

        /*
         * Static method to do the parsing
         */
    }

    internal sealed class MultipartContentElement
    {
        #region Fields

        private String _contentType;
        private HttpRawUploadedContent _data;
        private String _filename;
        private int _length;
        private String _name;
        private int _offset;

        #endregion Fields

        #region Constructors

        internal MultipartContentElement(String name, String filename, String contentType, HttpRawUploadedContent data, int offset, int length)
        {
            _name = name;
            _filename = filename;
            _contentType = contentType;
            _data = data;
            _offset = offset;
            _length = length;
        }

        #endregion Constructors

        #region Properties

        internal bool IsFile
        {
            get { return (_filename != null); }
        }

        internal bool IsFormItem
        {
            get { return (_filename == null); }
        }

        internal String Name
        {
            get { return _name; }
        }

        #endregion Properties

        #region Methods

        internal HttpPostedFile GetAsPostedFile()
        {
            return new HttpPostedFile(
                                     _filename,
                                     _contentType,
                                     new HttpInputStream(_data, _offset, _length));
        }

        internal String GetAsString(Encoding encoding)
        {
            if (_length > 0)
            {
                return encoding.GetString(_data.GetAsByteArray(_offset, _length));
            }
            else
            {
                return String.Empty;
            }
        }

        #endregion Methods
    }

    /*
     * Multipart content parser. Split content into elements.
     */
}
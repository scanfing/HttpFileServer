using System;
using System.Collections.Generic;
using System.Text;
using HttpFileServer.Models;

namespace HttpFileServer.Core
{
    public interface IFileServer
    {
        #region Events

        event EventHandler<string> LogGenerated;

        event EventHandler<RequestModel> NewReqeustIn;

        event EventHandler<RequestModel> RequestOut;

        #endregion Events

        #region Properties

        bool EnableUpload { get; }

        int Port { get; }

        string SourceDir { get; }

        #endregion Properties

        #region Methods

        void Start();

        void Stop();

        #endregion Methods
    }
}
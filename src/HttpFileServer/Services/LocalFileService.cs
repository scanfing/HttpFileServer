using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HttpFileServer.Services
{
    public class LocalFileService
    {
        #region Fields

        private CancellationTokenSource _cancelTokenSource;
        private FileSystemWatcher _watcher;

        #endregion Fields

        #region Constructors

        public LocalFileService(string dstDirPath)
        {
            _watcher = new FileSystemWatcher(dstDirPath);
            _watcher.IncludeSubdirectories = true;

            _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;

            _watcher.Created += _watcher_Created;
            _watcher.Deleted += _watcher_Deleted;
            _watcher.Renamed += _watcher_Renamed;
            _watcher.Changed += _watcher_Changed;
        }

        #endregion Constructors

        #region Events

        public event EventHandler<string> DirContentChanged;

        public event EventHandler<string> PathDeleted;

        #endregion Events

        #region Methods

        public void Start()
        {
            _cancelTokenSource = new CancellationTokenSource();
            _watcher.EnableRaisingEvents = true;
            Task.Factory.StartNew(() =>
             {
                 _watcher.WaitForChanged(WatcherChangeTypes.All);
             }, _cancelTokenSource.Token);
        }

        public void Stop()
        {
            _cancelTokenSource.Cancel();
            _watcher.EnableRaisingEvents = false;
        }

        private void _watcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (File.Exists(e.FullPath))
                RaiseParentDirContentChanged(e.FullPath);
            else
                RaiseDirContentChanged(e.FullPath);
        }

        private void _watcher_Created(object sender, FileSystemEventArgs e)
        {
            //created 不做任何处理 用户访问地址时才创建对应缓存
            //新建操作会触发父级路径的 _watcher_Changed 事件 在里面触发RaiseDirContentChanged事件
        }

        private void _watcher_Deleted(object sender, FileSystemEventArgs e)
        {
            RaisePathDeleted(e.FullPath);
        }

        private void _watcher_Renamed(object sender, RenamedEventArgs e)
        {
            RaisePathDeleted(e.OldFullPath);
            RaiseParentDirContentChanged(e.FullPath);
        }

        private void RaiseDirContentChanged(string dirPath)
        {
            DirContentChanged?.Invoke(this, dirPath);
        }

        private void RaiseParentDirContentChanged(string path)
        {
            var dir = Path.GetDirectoryName(path);
            RaiseDirContentChanged(dir);
        }

        private void RaisePathDeleted(string path)
        {
            PathDeleted?.Invoke(this, path);
        }

        #endregion Methods
    }
}
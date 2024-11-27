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

            _watcher.NotifyFilter = NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;

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
            // 部分操作不会实时触发Changed事件，会延后触发（大概是操作后的读取列表动作），
            // 直接暴力处理，不细化判断触发原因，RaiseParentDirContentChanged 会存在重复处理问题
            // 文件大小发生变化也需要刷新缓存
            RaiseParentDirContentChanged(e.FullPath);
        }

        private void _watcher_Created(object sender, FileSystemEventArgs e)
        {
            RaiseParentDirContentChanged(e.FullPath);
        }

        private void _watcher_Deleted(object sender, FileSystemEventArgs e)
        {
            RaisePathDeleted(e.FullPath);
            RaiseParentDirContentChanged(e.FullPath);
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
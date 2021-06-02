using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HttpFileServer.Services
{
    public class FileAccessHelper
    {
        #region Fields

        private static ConcurrentDictionary<string, ConcurrentQueue<DateTime>> _fileAccesserCount;

        #endregion Fields

        #region Constructors

        static FileAccessHelper()
        {
            _fileAccesserCount = new ConcurrentDictionary<string, ConcurrentQueue<DateTime>>();
        }

        #endregion Constructors

        #region Properties

        public static int LimitCount { get; set; } = 2;

        #endregion Properties

        #region Methods

        public static async Task AddAccessCount(string path)
        {
            ConcurrentQueue<DateTime> queue;
            if (!_fileAccesserCount.ContainsKey(path))
            {
                System.Diagnostics.Trace.WriteLine($"init queue for {path}");
                _fileAccesserCount.TryAdd(path, new ConcurrentQueue<DateTime>());
            }

            while (!_fileAccesserCount.TryGetValue(path, out queue))
            {
                await Task.Delay(10);
            }
            var count = LimitCount + 1;
            lock (queue)
            {
                count = queue.Count;
            }
            while (count >= LimitCount)
            {
                await Task.Delay(1);
                lock (queue)
                {
                    count = queue.Count;
                }
            }
            lock (queue)
            {
                System.Diagnostics.Trace.WriteLine($"{path} access count({queue.Count}) + 1");
                queue.Enqueue(DateTime.Now);
            }
        }

        public static async void SubAccessCount(string path)
        {
            ConcurrentQueue<DateTime> queue;
            while (!_fileAccesserCount.TryGetValue(path, out queue))
            {
                await Task.Delay(5);
            }
            lock (queue)
            {
                queue.TryDequeue(out var dt);
                System.Diagnostics.Trace.WriteLine($"{path} {queue.Count + 1} -1, en:{dt:HH:mm:ss.fff} out:{DateTime.Now:HH:mm:ss.fff}");
            }
        }

        #endregion Methods
    }
}
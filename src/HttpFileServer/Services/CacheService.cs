using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HttpFileServer.Services
{
    public class CacheService
    {
        #region Fields

        private static CacheService _defaultInstance;
        private ConcurrentDictionary<string, byte[]> _cacheDict;
        private ConcurrentDictionary<string, string> _cacheKeyDict;
        // 新增缓存元数据：时间戳和访问计数
        private ConcurrentDictionary<string, DateTime> _cacheTimeDict = new ConcurrentDictionary<string, DateTime>();
        private ConcurrentDictionary<string, long> _cacheAccessDict = new ConcurrentDictionary<string, long>();
        private const long MaxCacheFileSize =104857600; //100MB
        private const long MaxCachePoolSize =4294967296; //4GB

        #endregion Fields

        #region Constructors

        public CacheService()
        {
            _cacheDict = new ConcurrentDictionary<string, byte[]>();
            _cacheKeyDict = new ConcurrentDictionary<string, string>();
        }

        #endregion Constructors

        #region Methods

        public static CacheService GetDefault()
        {
            if (_defaultInstance == null)
                _defaultInstance = new CacheService();

            return _defaultInstance;
        }

        public void Clear()
        {
            _cacheDict.Clear();
            _cacheKeyDict.Clear();
            _cacheTimeDict.Clear();
            _cacheAccessDict.Clear();
        }

        public void Delete(string path)
        {
            var key = path.TrimEnd('\\');
            _cacheKeyDict.TryRemove(key, out _);
            _cacheDict.TryRemove(key, out _);
            _cacheTimeDict.TryRemove(key, out _);
            _cacheAccessDict.TryRemove(key, out _);
        }

        public byte[] GetCache(string path)
        {
            var key = path.TrimEnd('\\');
            if (_cacheDict.TryGetValue(key, out var buff))
            {
                //访问计数+1
                _cacheAccessDict.AddOrUpdate(key,1, (k, v) => v +1);
                return buff;
            }
            return null;
        }

        public string GetPathCacheId(string path)
        {
            var key = path.TrimEnd('\\');
            if (!_cacheKeyDict.TryGetValue(key, out var cid))
            {
                cid = Guid.NewGuid().ToString();
                _cacheKeyDict[path] = cid;
            }
            return cid;
        }

        public void SaveCache(string path, byte[] data)
        {
            var key = path.TrimEnd('\\');
            // 超过100MB不缓存
            if (data == null || data.LongLength > MaxCacheFileSize)
                return;
            // 淘汰机制：缓存池超过4GB时，移除最旧和访问量最低的缓存
            while (GetTotalCacheSize() + data.LongLength > MaxCachePoolSize)
            {
                // 按时间戳和访问量排序，优先移除最旧且访问量最低的
                var removeKey = _cacheTimeDict.OrderBy(x => x.Value)
                    .ThenBy(x => _cacheAccessDict.ContainsKey(x.Key) ? _cacheAccessDict[x.Key] :0)
                    .Select(x => x.Key).FirstOrDefault();
                if (removeKey == null) break;
                Delete(removeKey);
            }
            _cacheDict[key] = data;
            _cacheTimeDict[key] = DateTime.UtcNow;
            _cacheAccessDict[key] =1;
        }

        private long GetTotalCacheSize()
        {
            long total =0;
            foreach (var kv in _cacheDict)
            {
                if (kv.Value != null)
                    total += kv.Value.LongLength;
            }
            return total;
        }

        #endregion Methods
    }
}
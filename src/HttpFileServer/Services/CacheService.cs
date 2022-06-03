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
        }

        public void Delete(string path)
        {
            var key = path.TrimEnd('\\');
            _cacheKeyDict.TryRemove(key, out _);
            _cacheDict.TryRemove(key, out _);
        }

        public byte[] GetCache(string path)
        {
            var key = path.TrimEnd('\\');
            if (_cacheDict.TryGetValue(key, out var buff))
            {
                return buff.ToArray();
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
            _cacheDict[key] = data;
        }

        #endregion Methods
    }
}
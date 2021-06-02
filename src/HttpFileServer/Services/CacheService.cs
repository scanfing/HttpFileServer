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
            _cacheKeyDict.TryRemove(path, out _);
            _cacheDict.TryRemove(path, out _);
        }

        public byte[] GetCache(string path)
        {
            if (_cacheDict.TryGetValue(path, out var buff))
            {
                return buff.ToArray();
            }
            return null;
        }

        public string GetPathCacheId(string path)
        {
            if (!_cacheKeyDict.TryGetValue(path, out var key))
            {
                key = Guid.NewGuid().ToString();
                _cacheKeyDict[path] = key;
            }
            return key;
        }

        public void SaveCache(string path, byte[] data)
        {
            _cacheDict[path] = data;
        }

        #endregion Methods
    }
}
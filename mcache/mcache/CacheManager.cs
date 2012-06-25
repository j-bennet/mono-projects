using System;
using System.Diagnostics;
using System.IO;

namespace net.timka.mcache
{
	public enum StoreType
	{
		File,
		SqLite
	}

	public class CacheManager : ICache
	{
		#region constants

		private static readonly string FileCacheFolder = "Cache";
		private static string ApplicationBaseFolder = null;

		#endregion

		private CacheBase _cache;
		private static object _objInstance = new object();
		private StoreType _storeType = StoreType.SqLite;

		private CacheManager()
		{
			switch (_storeType)
			{
				case StoreType.File:
					_cache = new FileCache();
					break;
				case StoreType.SqLite:
					_cache = new SqliteCache();
					break;
				default:
					throw new Exception(string.Format("CacheManager .ctor failed: {0} type not handled", _storeType));
			}
		}

		public static CacheManager Instance
		{
			get 
			{
				lock (_objInstance)
				{
					return CacheManagerInstance.Instance;
				}
			}
		}

		public void Initialize(string applicationBaseFolder)
		{
			ApplicationBaseFolder = applicationBaseFolder;
		}

		public bool Add(string key, object value, DateTime? expires)
		{
			AssertInitialized();

			Debug.WriteLine(string.Format("*** CacheManager: add {0}", key));
			return _cache.Add(key, value, expires);
		}

		public object Get(string key)
		{
			AssertInitialized();

			Debug.WriteLine(string.Format("*** CacheManager: get {0}", key));
			return _cache.Get(key);
		}

		public void Remove(string key)
		{
			AssertInitialized();

			Debug.WriteLine(string.Format("*** CacheManager: remove {0}", key));
			_cache.Remove(key);
		}

		public object this[string key]
		{
			get
			{
				AssertInitialized();

				Debug.WriteLine(string.Format("*** CacheManager: this[{0}]", key));
				return _cache[key];
			}
		}

		public string[] GetKeys(string startsWith)
		{
			AssertInitialized();

			Debug.WriteLine(string.Format("*** CacheManager: get keys {0}", startsWith));
			return _cache.GetKeys(startsWith);
		}

		#region private methods

		private void AssertInitialized()
		{
			if (string.IsNullOrEmpty(ApplicationBaseFolder))
			{
				throw new ApplicationException("Application path not set.");
			}

			if (string.IsNullOrEmpty(_cache.CachePath))
			{
				string cachePath = Path.Combine(ApplicationBaseFolder, FileCacheFolder);
				CreateCachePath(cachePath);
				_cache.CachePath = cachePath;
			}
		}

		private void CreateCachePath(string path)
		{
			if (!Directory.Exists(path))
			{
				Directory.CreateDirectory(path);
			}
			Debug.WriteLine(string.Format("*** CacheManager: set CachePath to {0}", path));
		}

		#endregion

		#region Singleton helper

		private sealed class CacheManagerInstance
		{
			private static readonly CacheManager _instance = new CacheManager();

			public static CacheManager Instance
			{
				get { return _instance; }
			}
		}

		#endregion
	}
}
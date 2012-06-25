using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using net.timka.mcache.Utils;

namespace net.timka.mcache
{
	public abstract class CacheBase : ICache, IDisposable
	{
		#region constants

		private const int ExpirationThreadInterval = 60 * 1000;

		#endregion

		protected static string _cachePath = string.Empty;
		protected Thread _cacheThread = null;
		protected bool _threadWorking = false;

		public string CachePath
		{
			get { return _cachePath; }
			set
			{
				_cachePath = value;
				AssertExpirationThread();
			}
		}

		public abstract bool Add(string key, object value, DateTime? expires);
		public abstract object Get(string key);
		public abstract void Remove(string key);

		public object this[string key]
		{
			get { return Get(key); }
		}

		public virtual void Dispose()
		{
			if (_cacheThread != null)
			{
				_cacheThread.Abort();
				_cacheThread = null;
			}
		}

		#region protected methods

		protected abstract void ExpireItems();
		
		protected internal abstract string[] GetKeys(string startsWith);

		protected void AssertExpirationThread()
		{
			Debug.WriteLine("*** CacheManager: assert expiration thread");

			if (!string.IsNullOrEmpty(_cachePath) && _cacheThread == null)
			{
				Task.Factory.StartNew(CreateExpirationThread);
			}
		}

		protected void AssertNotEmpty(string key)
		{
			if (string.IsNullOrEmpty(key))
			{
				throw new ArgumentNullException("key");
			}
		}

		protected void AssertInitialized()
		{
			if (string.IsNullOrEmpty(_cachePath))
			{
				throw new ApplicationException("Cache path not set.");
			}
		}

		#endregion

		#region Cache updater thread

		private void CreateExpirationThread()
		{
			Debug.WriteLine("*** CacheManager: creating expiration thread");

			_cacheThread = new Thread(CheckExpiredItems);
			_cacheThread.Priority = ThreadPriority.BelowNormal;
			_cacheThread.IsBackground = true;
			_cacheThread.Start();

			_threadWorking = true;

			Debug.WriteLine("*** CacheManager: expiration thread created");
		}

		private void CheckExpiredItems()
		{
			try
			{
				while (_threadWorking)
				{
					try
					{
						DateTime tm = DateTime.Now;
						Debug.WriteLine("*** CacheManager: expire items started");

						ExpireItems();

						Debug.WriteLine(string.Format("*** CacheManager: expire items ended, seconds: {0}", TimeUtils.Elapsed(tm)));

						Thread.Sleep(ExpirationThreadInterval);
					}
					catch (Exception ex)
					{
						Debug.WriteLine(ex.ToString());
					}
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.ToString());
			}
			finally
			{
				_cacheThread = null;
			}
		}

		#endregion

	}
}
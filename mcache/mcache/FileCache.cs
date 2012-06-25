using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace net.timka.mcache
{
	public class FileCache : CacheBase
	{
		public override bool Add(string key, object value, DateTime? expires)
		{
			AssertInitialized();
			AssertNotEmpty(key);

			string fileName = CreateSafeName(key);
			string fullPath = Path.Combine(_cachePath, fileName);

			if (!File.Exists(fullPath))
			{
				using (FileStream fs = File.OpenWrite(fullPath))
				{
					var item = new CacheItem { Item = value };
					if (expires.HasValue)
					{
						item.Expires = expires.Value.ToUniversalTime();
					}
					var formatter = new BinaryFormatter();
					formatter.Serialize(fs, item);
				}
				return true;
			}
			return false;
		}

		public override object Get(string key)
		{
			AssertInitialized();
			AssertNotEmpty(key);

			string fileName = CreateSafeName(key);
			string fullPath = Path.Combine(_cachePath, fileName);

			CacheItem item = null;
			if (File.Exists(fullPath))
			{
				try
				{					
					using (FileStream fs = File.OpenRead(fullPath))
					{
						var formatter = new BinaryFormatter();
						item = formatter.Deserialize(fs) as CacheItem;
					}
				}
				catch (Exception x)
				{
					Debug.WriteLine(string.Format("File {0} could not be deserialized. Removing...", fullPath));
					Debug.WriteLine(x.ToString());
					File.Delete(fullPath);
				}
			}

			if (item != null && item.Expires.HasValue && item.Expires.Value.Ticks <= DateTime.Now.ToUniversalTime().Ticks)
			{
				Remove(key);
				item = null;
			}

			return (item == null) ? null : item.Item;
		}

		public override void Remove(string key)
		{
			AssertInitialized();
			AssertNotEmpty(key);

			string fileName = CreateSafeName(key);
			string fullPath = Path.Combine(_cachePath, fileName);

			if (File.Exists(fullPath))
			{
				File.Delete(fullPath);
			}
		}

		private string CreateSafeName(string key)
		{
			string safeName = key;

			if (!string.IsNullOrEmpty(key))
			{
				char[] notsafe = Path.GetInvalidFileNameChars();

				foreach (char c in notsafe)
				{
					safeName = safeName.Replace(c, '_');
				}
				safeName = string.Format("{0}.dat", safeName);
			}
			return safeName;
		}

		#region protected methods

		protected internal override string[] GetKeys(string startsWith)
		{
			string pattern = startsWith + "*";
			string[] files = Directory.GetFiles(_cachePath, pattern);
			if (files != null)
			{
				for (int i = 0; i < files.Length; i++)
				{
					files[i] = Path.GetFileNameWithoutExtension(files[i]);
				}
			}
			return files;
		}

		protected override void ExpireItems()
		{
			string[] files = Directory.GetFiles(_cachePath);

			if (files != null)
			{
				foreach (string file in files)
				{
					string key = Path.GetFileNameWithoutExtension(file);
					var dummyObject = Get(key);
					if (dummyObject == null)
					{
						Debug.WriteLine(string.Format("*** Cached item expired: {0}", key));
					}
				}
			}
		}

		#endregion
	}
}
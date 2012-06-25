using System;

namespace net.timka.mcache
{
	[Serializable]
	public class CacheItem
	{
		public object Item;
		public DateTime? Expires;
	}
}
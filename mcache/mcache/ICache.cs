using System;

namespace net.timka.mcache
{
	public interface ICache
	{
		bool Add(string key, object value, DateTime? expires);
		
		object Get(string key);
		
		void Remove(string key);

		object this[string key] { get; }
	}
}
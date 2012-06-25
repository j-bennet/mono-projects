using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Mono.Data.Sqlite;

namespace net.timka.mcache
{
	public class SqliteCache : CacheBase
	{
		#region constants

		private const string DbFileName = "sqlitecache.db3";
		private static SqliteConnection _connection = null;
		private static object _lock = new object();

		#endregion

		# region properties

		private static SqliteConnection Connection
		{
			get
			{
				if (_connection == null)
				{
					lock (_lock)
					{
						_connection = GetConnection();
					}
				}
				return _connection;
			}
		}

		#endregion

		#region public methods

		public override bool Add(string key, object value, DateTime? expires)
		{
			AssertInitialized();
			AssertNotEmpty(key);

			using (var c = Connection.CreateCommand())
			{
				c.CommandText = "select count(*) from [cacheitems] where key = @key";
				c.Parameters.AddWithValue("@key", key);
				int count = Convert.ToInt32(c.ExecuteScalar());
				if (count > 0)
				{
					return false;
				}
			}
			using (var c = Connection.CreateCommand())
			{
				byte[] bytes = SerializeItem(value);
				c.CommandText = "insert into [cacheitems] ([key], [expires], [binarydata]) values (@key, @expires, @binarydata)";
				c.Parameters.AddWithValue("@key", key);
				if (expires.HasValue)
				{
					long ticks = expires.Value.ToUniversalTime().Ticks;
					c.Parameters.AddWithValue("@expires", ticks);
				}
				else
				{
					c.Parameters.AddWithValue("@expires", null);
				}
				c.Parameters.AddWithValue("@binarydata", bytes);
				c.ExecuteNonQuery();

				return true;
			}
		}

		public override object Get(string key)
		{
			AssertInitialized();
			AssertNotEmpty(key);

			if (!DatabaseExists())
			{
				return null;
			}
			
			object value = null;

			using (var c = Connection.CreateCommand())
			{
				c.CommandText = "select [binarydata] from [cacheitems] where key = @key";
				c.Parameters.AddWithValue("@key", key);
				byte[] data = (byte[]) c.ExecuteScalar();
				if (data != null)
				{
					value = DeserializeItem(data);
				}
			}
			return value;
		}

		public override void Remove(string key)
		{
			AssertInitialized();
			AssertNotEmpty(key);

			using (var c = Connection.CreateCommand())
			{
				c.CommandText = "delete from [cacheitems] where key = @key";
				c.Parameters.AddWithValue("@key", key);
				c.ExecuteNonQuery();
			}
		}

		public override void Dispose()
		{
			base.Dispose();
			if (_connection != null)
			{
				_connection.Close();
				_connection = null;
			}
		}

		#endregion

		#region protected methods

		protected internal override string[] GetKeys(string startsWith)
		{
			AssertInitialized();

			if (!DatabaseExists())
			{
				return new string[0];
			}

			try
			{
				using (var c = Connection.CreateCommand())
				{
					c.CommandText = string.Format("select [key] from [cacheitems] where key like '{0}%'", startsWith);
					using (SqliteDataReader reader = c.ExecuteReader())
					{
						List<string> keys = new List<string>();
						while (reader.Read())
						{
							keys.Add(reader.GetString(0));
						}
						return keys.ToArray();
					}
				}
			}
			catch (Exception x)
			{
				Debug.WriteLine("*** SQLiteCache: GetKeys:  " + startsWith);
				Debug.WriteLine(x.ToString());

				Debug.WriteLine("*** SQLiteCache: delete corrupted db file....");
				string fileName = Path.Combine(_cachePath, DbFileName);
				bool exists = File.Exists(fileName);
	
				if (exists)
				{
					File.Delete(fileName);
				}

			}
			return new string[0];
		}

		protected override void ExpireItems()
		{
			try
			{
				AssertInitialized();

				if (!DatabaseExists())
				{
					Debug.WriteLine("*** SQLiteCache: no data to expire");
					return;
				}

				RemoveExpiredItems();
			}
			catch (Exception x)
			{
				Debug.WriteLine("*** SQLiteCache: ExpireItems: exception");
				Debug.WriteLine(x.ToString());
			}
			catch
			{
				Debug.WriteLine("*** SQLiteCache: ExpireItems: unknown exception");
			}
		}

		#endregion

		#region private methods

		private static void RemoveExpiredItems()
		{
			long ticks = DateTime.Now.ToUniversalTime().Ticks;
			int count = 0;

			using (var c = Connection.CreateCommand())
			{
				c.CommandText = "select count(*) from [cacheitems] where ([expires] is not null) and ([expires] <= @currentTicks)";
				c.Parameters.AddWithValue("@currentTicks", ticks);
				count = Convert.ToInt32(c.ExecuteScalar());
				Debug.WriteLine(string.Format("*** SQLiteCache: ExpireItems: found {0} expired items", count));
			}

			if (count > 0)
			{
				using (var c = Connection.CreateCommand())
				{
					c.CommandText = "delete from [cacheitems] where ([expires] is not null) and ([expires] <= @currentTicks)";
					c.Parameters.AddWithValue("@currentTicks", ticks);
					int rowCount = c.ExecuteNonQuery();
					Debug.WriteLine(string.Format("*** SQLiteCache: ExpireItems: removed {0} items", rowCount));
				}
			}
		}

		private object DeserializeItem(byte[] data)
		{
			using (MemoryStream ms = new MemoryStream(data))
			{
				var formatter = new BinaryFormatter();
				return formatter.Deserialize(ms);
			}
		}

		private byte[] SerializeItem(object value)
		{
			using (MemoryStream ms = new MemoryStream())
			{
				var formatter = new BinaryFormatter();
				formatter.Serialize(ms, value);
				return ms.GetBuffer();
			}
		}

		private static SqliteConnection GetConnection()
		{
			Debug.WriteLine(string.Format("*** SQLiteCache: requesting connection..."));

			string fileName = Path.Combine(_cachePath, DbFileName);
			string connectionString = string.Format("Data Source={0}", fileName);
			bool exists = File.Exists(fileName);

			if (!exists)
			{
				SqliteConnection.CreateFile(fileName);
				Debug.WriteLine(string.Format("*** SQLiteCache: CreateFile {0}", fileName));
			}

			var connection = new SqliteConnection(connectionString);
			connection.Open();

			if (!exists)
			{
				Debug.WriteLine(string.Format("*** SQLiteCache: CreateDatabase: {0}", connectionString));
				CreateDatabase(connection);
			}

			Debug.WriteLine(string.Format("*** SQLiteCache: connection: {0}", connectionString));

			return connection;
		}

		private bool DatabaseExists()
		{
			string fileName = Path.Combine(_cachePath, DbFileName);
			bool exists = File.Exists(fileName);
			return exists;
		}

		private static void CreateDatabase(SqliteConnection connection)
		{
			string[] commandText = new[]
			{
			    "create table if not exists [cacheitems] ([id] integer primary key not null, [key] ntext not null, [expires] bigint, [binarydata] blob);",
			    "create unique index if not exists ix_key on [cacheitems] ([key]);"
			};
			for (int i = 0; i < commandText.Length; i++)
			{
				using (SqliteCommand c = connection.CreateCommand())
				{
					c.CommandText = commandText[i];
					c.ExecuteNonQuery();

					Debug.WriteLine(string.Format("*** SQLiteCache: EXECUTED: {0}", commandText[i]));
				}
			}
		}

		#endregion
	}
}
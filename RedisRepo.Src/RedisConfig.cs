using System;

using StackExchange.Redis;

namespace RedisRepo.Src
{
	/// <summary>
	/// Use this class to define the configuration of the Redis server. It is HIGHLY recommended that this class be 
	/// a singleton.
	/// </summary>
	public class RedisConfig
	{
		private ConnectionMultiplexer _redisMultiplexer;
		private readonly Object _thisLock = new object();

		public RedisConfig(int redisDatabaseId, string redisConnectionString)
		{
			RedisDatabaseId = redisDatabaseId;
			RedisConnectionString = redisConnectionString;
		}

		public string RedisConnectionString { get; private set; }

		public int RedisDatabaseId { get; private set; }

		public ConnectionMultiplexer GetRedisMultiplexer()
		{
			if (_redisMultiplexer != null)
				return _redisMultiplexer;
			lock (_thisLock)
			{
				_redisMultiplexer = ConnectionMultiplexer.Connect(RedisConnectionString);
			}
			return _redisMultiplexer;
		} 
	}
}
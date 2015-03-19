using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using StackExchange.Redis;

namespace RedisRepo.Src
{
	public class RedisCache : IAppCache
	{
		private const string AllPartitionsCacheKey = "AllPartitionNames";
		private const string TimeoutKeySuffix = ":TimeoutItems";
		private readonly IDatabase _redisDatabase;
		private readonly RedisConfig _redisConfig;

		public RedisCache(RedisConfig redisConfig)
		{
			_redisConfig = redisConfig;
			_redisDatabase = _redisConfig.GetRedisMultiplexer().GetDatabase(_redisConfig.RedisDatabaseId);
		}

		public bool Contains(string key, string partitionName = "") { return !string.IsNullOrEmpty(key) && ContainsAsync(key, partitionName).Result; }

		public async Task<bool> ContainsAsync(string key, string partitionName = "")
		{
			if (string.IsNullOrEmpty(key))
				return false;
			if (string.IsNullOrEmpty(partitionName))
				return await _redisDatabase.KeyExistsAsync(key).ConfigureAwait(false);
			var partitionKey = ComposePartitionKey(partitionName);
			return await _redisDatabase.HashExistsAsync(partitionKey, key).ConfigureAwait(false);
		}

		public object Get(string key, string partitionName = "") { return string.IsNullOrEmpty(key) ? null : GetAsync(key, partitionName).Result; }

		public async Task<object> GetAsync(string key, string partitionName = "")
		{
			if (string.IsNullOrEmpty(key))
				return null;
			RedisValue cacheValue;
			if (string.IsNullOrEmpty(partitionName))
			{
				cacheValue = await _redisDatabase.StringGetAsync(key).ConfigureAwait(false);
				if (cacheValue.IsNullOrEmpty)
					return null;
			}
			else
			{
				var partitionKey = ComposePartitionKey(partitionName);

				// Check to see if the value has timed out
				var timeout = await GetTimeoutValueAsync(partitionName, key).ConfigureAwait(false);
				if (timeout != default(DateTimeOffset) && timeout < DateTimeOffset.UtcNow)
				{
					await RemoveExpiredItemsFromPartitionAsync(partitionName).ConfigureAwait(false);
					return null;
				}

				// Retrieve the actual RedisValue
				cacheValue = await _redisDatabase.HashGetAsync(partitionKey, key).ConfigureAwait(false);

				// If we have a null value, return null
				if (cacheValue.IsNullOrEmpty)
					return null;
			}
			var objValue = JsonConvert.DeserializeObject(cacheValue);
			return objValue;
		}

		public TValue GetValue<TValue>(string key, string partitionName = "") where TValue : class
		{
			return string.IsNullOrEmpty(key) ? null : GetValueAsync<TValue>(key, partitionName).Result;
		}

		public async Task<TValue> GetValueAsync<TValue>(string key, string partitionName = "") where TValue : class
		{
			if (string.IsNullOrEmpty(key))
				return null;
			RedisValue cachedValue;
			if (string.IsNullOrEmpty(partitionName))
			{
				cachedValue = await _redisDatabase.StringGetAsync(key).ConfigureAwait(false);
				if (cachedValue.IsNullOrEmpty)
					return null;
			}
			else
			{
				var partitionKey = ComposePartitionKey(partitionName);

				// Check to see if the value has timed out
				var timeout = await GetTimeoutValueAsync(partitionName, key).ConfigureAwait(false);
				if (timeout != default(DateTimeOffset) && timeout < DateTimeOffset.UtcNow)
				{
					await RemoveExpiredItemsFromPartitionAsync(partitionName).ConfigureAwait(false);
					return null;
				}

				// Retrieve the actual RedisValue
				cachedValue = await _redisDatabase.HashGetAsync(partitionKey, key).ConfigureAwait(false);
				if (cachedValue.IsNullOrEmpty)
					return null;
			}
			var result = JsonConvert.DeserializeObject<TValue>(cachedValue);
			return result;
		}

		public async Task<List<string>> GetAllPartitionNamesAsync()
		{
			var allPartitionNames = await _redisDatabase.SetMembersAsync(AllPartitionsCacheKey).ConfigureAwait(false);
			var partitions = allPartitionNames.Select(partitionName => partitionName.ToString()).ToList();
			return partitions;
		}

		public List<TValue> Find<TValue>(string indexName, string indexValue, string partitionName = "") where TValue : class
		{
			if (string.IsNullOrEmpty(indexName) || string.IsNullOrEmpty(indexValue))
				return new List<TValue>();
			return FindAsync<TValue>(indexName, indexValue, partitionName).Result;
		}

		public async Task<List<TValue>> FindAsync<TValue>(string indexName, string indexValue, string partitionName = "") where TValue : class
		{
			if (string.IsNullOrEmpty(indexName) || string.IsNullOrEmpty(indexValue))
				return new List<TValue>();
			var indexKey = ComposeKeyForCustomIndex(indexName, indexValue);
			List<TValue> indexValues;
			if (string.IsNullOrEmpty(partitionName))
				indexValues = await GetAllItemsFromSetAsync<TValue>(indexKey).ConfigureAwait(false);
			//indexValues = await GetAllItemsFromHashAsync<TValue>(indexKey).ConfigureAwait(false);
			else
			{
				var partitionKey = ComposePartitionKey(partitionName);
				await RemoveExpiredItemsFromPartitionAsync(partitionKey).ConfigureAwait(false);
				indexValues = await GetAllItemsFromHashAsync<TValue>(indexKey).ConfigureAwait(false);
			}
			return indexValues;
		}

		public void AddOrUpdate(string key, object value, TimeSpan? timeout, string partitionName = "")
		{
			if (string.IsNullOrEmpty(key) || value == null)
				return;
			AddOrUpdateAsync(key, value, timeout, partitionName).Wait();
		}

		public async Task AddOrUpdateAsync(string key, object value, TimeSpan? timeout, string partitionName = "")
		{
			if (string.IsNullOrEmpty(key) || value == null)
				return;
			var cacheValue = JsonConvert.SerializeObject(value);
			if (string.IsNullOrEmpty(partitionName))
			{
				if (timeout == null)
					await _redisDatabase.StringSetAsync(key, cacheValue).ConfigureAwait(false);
				else
					await _redisDatabase.StringSetAsync(key, cacheValue, timeout).ConfigureAwait(false);
			}
			else
			{
				var partitionKey = ComposePartitionKey(partitionName);
				await _redisDatabase.SetAddAsync(AllPartitionsCacheKey, partitionKey).ConfigureAwait(false);
				await _redisDatabase.HashSetAsync(partitionKey, key, cacheValue).ConfigureAwait(false);
				await AddTimeoutToPartitionAsync(partitionName, key, timeout).ConfigureAwait(false);
			}
		}

		public void AddOrUpdateItemOnCustomIndex(string indexName, string indexedValue, string indexedObjectCacheKey, string partitionName = "")
		{
			AddOrUpdateItemOnCustomIndexAsync(indexName, indexedValue, indexedObjectCacheKey).Wait();
		}

		public async Task AddOrUpdateItemOnCustomIndexAsync(string indexName, string indexedValue, string indexedObjectCacheKey,
															string partitionName = "")
		{
			var indexCacheKey = ComposeKeyForCustomIndex(indexName, indexedValue);
			if (string.IsNullOrEmpty(partitionName))
				await _redisDatabase.SetAddAsync(indexCacheKey, indexedObjectCacheKey).ConfigureAwait(false);
			//await _redisDatabase.HashSetAsync(indexCacheKey, indexedObjectCacheKey, indexedObjectCacheKey).ConfigureAwait(false);
			else
			{
				var partitionKey = ComposePartitionKey(partitionName);
				await _redisDatabase.HashSetAsync(indexCacheKey, partitionKey, indexedObjectCacheKey).ConfigureAwait(false);
			}
		}

		public void Remove(string key, string partitionName = "")
		{
			if (string.IsNullOrEmpty(key))
				return;
			RemoveAsync(key, partitionName).Wait();
		}

		public async Task RemoveAsync(string key, string partitionName = "")
		{
			if (string.IsNullOrEmpty(key))
				return;

			// Remove it from the partition
			if (string.IsNullOrEmpty(partitionName))// Remove the object from regular cache
				await _redisDatabase.KeyDeleteAsync(key).ConfigureAwait(false);
			else// Else remove the object from the redis hash set.
			{
				var partitionKey = ComposePartitionKey(partitionName);
				await _redisDatabase.HashDeleteAsync(partitionKey, key).ConfigureAwait(false);
				var timeoutPartitionKey = ComposeTimeoutPartitionKey(partitionName);
				await _redisDatabase.HashDeleteAsync(timeoutPartitionKey, key).ConfigureAwait(false);
			}
		}

		public List<TValue> GetAllItemsInPartition<TValue>(string partitionName) where TValue : class
		{
			return string.IsNullOrEmpty(partitionName) ? new List<TValue>() : GetAllItemsInPartitionAsync<TValue>(partitionName).Result;
		}

		public async Task<List<TValue>> GetAllItemsInPartitionAsync<TValue>(string partitionName) where TValue : class
		{
			if (string.IsNullOrEmpty(partitionName))
				return new List<TValue>();
			var partitionKey = ComposePartitionKey(partitionName);
			var partitionHash = await _redisDatabase.HashGetAllAsync(partitionKey).ConfigureAwait(false);
			return partitionHash
				.Select(hashEntry => JsonConvert.DeserializeObject<TValue>(hashEntry.Value))
				.Where(obj => obj != default(TValue)).ToList();
		}

		public void RemoveFromCustomIndex(string indexName, string indexedValue, string cacheKey, string partitionName = "")
		{
			if (string.IsNullOrEmpty(indexName) || string.IsNullOrEmpty(indexedValue) || string.IsNullOrEmpty(cacheKey))
				return;
			RemoveFromCustomIndexAsync(indexName, indexedValue, cacheKey, partitionName).Wait();
		}

		public async Task RemoveFromCustomIndexAsync(string indexName, string indexedValue, string cacheKey, string partitionName = "")
		{
			if (string.IsNullOrEmpty(indexName) || string.IsNullOrEmpty(indexedValue) || string.IsNullOrEmpty(cacheKey))
				return;
			var indexCacheKey = ComposeKeyForCustomIndex(indexName, indexedValue);
			if (string.IsNullOrEmpty(partitionName))
				await _redisDatabase.SetRemoveAsync(indexCacheKey, cacheKey).ConfigureAwait(false);
			//await _redisDatabase.HashDeleteAsync(indexCacheKey, cacheKey).ConfigureAwait(false);
			else
			{
				var partitionKey = ComposePartitionKey(partitionName);
				await _redisDatabase.HashDeleteAsync(indexCacheKey, partitionKey).ConfigureAwait(false);
			}
		}

		public async Task RemoveExpiredItemsFromPartitionAsync(string partitionName)
		{
			var partitionKey = ComposePartitionKey(partitionName);
			var partitionTimeoutCacheKey = ComposeTimeoutPartitionKey(partitionName);
			var partitionTimeoutItems = await _redisDatabase.HashGetAllAsync(partitionTimeoutCacheKey).ConfigureAwait(false);
			if (partitionTimeoutItems.Length < 1)
				return;
			var expiredKeys = new List<RedisValue>();
			foreach (var hashEntry in partitionTimeoutItems)
			{
				var expirationDate = JsonConvert.DeserializeObject<DateTimeOffset>(hashEntry.Value);
				if (expirationDate < DateTimeOffset.UtcNow)
					expiredKeys.Add(hashEntry.Name);
			}
			if (expiredKeys.Count < 1)
				return;
			await _redisDatabase.HashDeleteAsync(partitionKey, expiredKeys.ToArray()).ConfigureAwait(false);
			await _redisDatabase.HashDeleteAsync(partitionTimeoutCacheKey, expiredKeys.ToArray()).ConfigureAwait(false);
		}

		public void ClearCache()
		{
			var endpoints = ConnectionMultiplexer.Connect(_redisConfig.RedisConnectionString).GetEndPoints();
			foreach (var server in endpoints.Select(endpoint => ConnectionMultiplexer.Connect(_redisConfig.RedisConnectionString).GetServer(endpoint)))
			{
				server.FlushDatabase(_redisConfig.RedisDatabaseId);
			}
		}

		public async Task ClearCacheAsync()
		{
			var connection = await ConnectionMultiplexer.ConnectAsync(_redisConfig.RedisConnectionString).ConfigureAwait(false);
			var endpoints = connection.GetEndPoints();
			foreach (var endPoint in endpoints)
			{
				var serverConnect = await ConnectionMultiplexer.ConnectAsync(_redisConfig.RedisConnectionString).ConfigureAwait(false);
				var server = serverConnect.GetServer(endPoint);
				await server.FlushDatabaseAsync(_redisConfig.RedisDatabaseId).ConfigureAwait(false);
			}
		}

		public string ComposeKeyForCustomIndex(string indexName, string indexValue)
		{
			if (string.IsNullOrEmpty(indexName) || string.IsNullOrEmpty(indexValue))
				return null;
			var hashedIndexedValue = ComputeBasicHash(indexValue);
			return string.Format("{0}:{1}:{2}", "Custom_Index_For", indexName, hashedIndexedValue);
		}

		private async Task AddTimeoutToPartitionAsync(string partitionName, string itemKey, TimeSpan? timeout)
		{
			if (timeout == null || string.IsNullOrEmpty(partitionName) || string.IsNullOrEmpty(itemKey))
				return;
			var validTimeout = (TimeSpan)timeout;
			var timeoutExpiration = DateTimeOffset.UtcNow.Add(validTimeout);
			var redisTimeoutValue = JsonConvert.SerializeObject(timeoutExpiration);
			var partitionTimeoutCacheKey = ComposeTimeoutPartitionKey(partitionName);
			await _redisDatabase.HashSetAsync(partitionTimeoutCacheKey, itemKey, redisTimeoutValue).ConfigureAwait(false);
		}

		private async Task<DateTimeOffset> GetTimeoutValueAsync(string partitionName, string itemKey)
		{
			if (string.IsNullOrEmpty(partitionName) || string.IsNullOrEmpty(itemKey))
				return default(DateTimeOffset);
			var timeoutPartitionKey = ComposeTimeoutPartitionKey(partitionName);
			var hashVal = await _redisDatabase.HashGetAsync(timeoutPartitionKey, itemKey).ConfigureAwait(false);
			if (hashVal.IsNullOrEmpty)
				return default(DateTimeOffset);
			var dateStamp = JsonConvert.DeserializeObject<DateTimeOffset>(hashVal);
			return dateStamp;
		}

		private async Task<List<TValue>> GetAllItemsFromHashAsync<TValue>(string hashCacheKey) where TValue : class
		{
			if (string.IsNullOrEmpty(hashCacheKey))
				return new List<TValue>();
			if (!_redisDatabase.KeyExists(hashCacheKey))
				return new List<TValue>();
			var hashMembers = await _redisDatabase.HashGetAllAsync(hashCacheKey).ConfigureAwait(false);
			if (hashMembers.Length < 1)
				return new List<TValue>();
			var resultList = new List<TValue>();
			foreach (var hashMember in hashMembers)
			{
				// Get the json object out of the partition first
				var redisVal = await _redisDatabase.HashGetAsync(hashMember.Name.ToString(), hashMember.Value).ConfigureAwait(false);
				if (redisVal.IsNullOrEmpty)
				{
					await _redisDatabase.HashDeleteAsync(hashCacheKey, hashMember.Name).ConfigureAwait(false);
					continue;
				}

				// Since we have a json value we deserialize it and add it to the list of return items.
				var tValueItem = JsonConvert.DeserializeObject<TValue>(redisVal);
				if (tValueItem == default(TValue))
				{
					await _redisDatabase.HashDeleteAsync(hashCacheKey, hashMember.Name).ConfigureAwait(false);
					continue;
				}
				resultList.Add(tValueItem);
			}
			return resultList;
		}

		private async Task<List<TValue>> GetAllItemsFromSetAsync<TValue>(string setCacheKey) where TValue : class
		{
			if (string.IsNullOrEmpty(setCacheKey))
				return new List<TValue>();
			if (!_redisDatabase.KeyExists(setCacheKey))
				return new List<TValue>();
			var setMembers = await _redisDatabase.SetMembersAsync(setCacheKey).ConfigureAwait(false);
			if (setMembers.Length < 1)
				return new List<TValue>();
			var resultList = new List<TValue>();
			foreach (var member in setMembers)
			{
				var cachedItem = await GetValueAsync<TValue>(member.ToString()).ConfigureAwait(false);
				if (cachedItem == default(TValue))
				{
					await _redisDatabase.SetRemoveAsync(setCacheKey, member).ConfigureAwait(false);
					continue;
				}
				resultList.Add(cachedItem);
			}
			return resultList;
		}

		private static string ComposePartitionKey(string partitionName) { return string.Format("{0}:{1}", "PartitionKey", partitionName); }

		private static string ComposeTimeoutPartitionKey(string partitionName)
		{
			var partitionKey = ComposePartitionKey(partitionName);
			return string.Format("{0}{1}", partitionKey, TimeoutKeySuffix);
		}

		private static string ComputeBasicHash(string textToHash, string salt = "")
		{
			if (string.IsNullOrEmpty(textToHash))
				return null;
			var hashAlgorithm = new SHA1Managed();
			var hash = hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(textToHash + salt));
			var hashHexString = BytesToHexString(hash);
			return hashHexString;
		}

		/// <summary>
		///     Converts a given byte array into a hexadecimal string.
		/// </summary>
		private static string BytesToHexString(byte[] byteArray)
		{
			return byteArray == null ? null : new SoapHexBinary(byteArray).ToString();
		}
	}
}
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RedisRepo.Src
{
	public interface IAppCache
	{
		bool Contains(string key, string partitionName = "");
		Task<bool> ContainsAsync(string key, string partitionName = "");

		object Get(string key, string partitionName = "");
		Task<object> GetAsync(string key, string partitionName = "");

		TValue GetValue<TValue>(string key, string partitionName = "") where TValue : class;
		Task<TValue> GetValueAsync<TValue>(string key, string partitionName = "") where TValue : class;

		Task<List<string>> GetAllPartitionNamesAsync();

		List<TValue> GetAllItemsInPartition<TValue>(string partitionName) where TValue : class;
		Task<List<TValue>> GetAllItemsInPartitionAsync<TValue>(string partitionName) where TValue : class;

		List<TValue> Find<TValue>(string indexName, string indexValue, string partitionName = "") where TValue : class;
		Task<List<TValue>> FindAsync<TValue>(string indexName, string indexValue, string partitionName = "") where TValue : class;

		void AddOrUpdate(string key, object value, TimeSpan? timeout, string partitionName = "");
		Task AddOrUpdateAsync(string key, object value, TimeSpan? timeout, string partitionName = "");

		void AddOrUpdateItemOnCustomIndex(string indexName, string indexedValue, string indexedObjectCacheKey, string partitionName = "");
		Task AddOrUpdateItemOnCustomIndexAsync(string indexName, string indexedValue, string indexedObjectCacheKey, string partitionName = "");

		void Remove(string key, string partitionName = "");
		Task RemoveAsync(string key, string partitionName = "");

		void RemoveFromCustomIndex(string indexName, string indexedValue, string cacheKey, string partitionName = "");
		Task RemoveFromCustomIndexAsync(string indexName, string indexedValue, string cacheKey, string partitionName = "");

		Task RemoveExpiredItemsFromPartitionAsync(string partitionName);

		void ClearCache();
		Task ClearCacheAsync();

		string ComposeKeyForCustomIndex(string indexName, string indexValue);
	}
}
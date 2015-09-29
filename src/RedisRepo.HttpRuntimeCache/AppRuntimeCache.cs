using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Caching;

using RedisRepo.Src;

namespace RedisRepo.HttpRuntimeCache
{
	public class AppRuntimeCache : IAppCache
	{
		private const string AllPartitionsCacheKey = "AllPartitionNames";

		private readonly TimeSpan _indexSlidingExpiration = TimeSpan.FromDays(364);

		private static Cache AppCache { get { return HttpRuntime.Cache; } }

		public bool Contains(string key, string partitionName = "")
		{
			if (string.IsNullOrEmpty(key))
				return false;
			var obj = AppCache.Get(key);
			return obj != null;
		}

		public async Task<bool> ContainsAsync(string key, string partitionName = "") { return await Task.Run(() => Contains(key)).ConfigureAwait(false); }

		public object Get(string key, string partitionName = "")
		{
			if (string.IsNullOrEmpty(key))
				return null;
			var resultObj = AppCache.Get(key);
			if (string.IsNullOrEmpty(partitionName))
				return resultObj;
			var indexList = AppCache.Get(ComposePartitionKey(partitionName)) as List<string> ?? new List<string>();
			if (indexList.Count <= 0)
				return resultObj;
			if (resultObj != null && indexList.All(reg => reg != key))
				indexList.Add(key);
			if (resultObj == null && indexList.Any(reg => reg == key))
				indexList.RemoveAll(reg => reg == key);
			AppCache.Insert(ComposePartitionKey(partitionName), indexList, null, Cache.NoAbsoluteExpiration, _indexSlidingExpiration);
			return resultObj;
		}

		public async Task<object> GetAsync(string key, string partitionName = "")
		{
			return await Task.Run(() => Get(key, partitionName)).ConfigureAwait(false);
		}

		public TValue GetValue<TValue>(string key, string partitionName = "") where TValue : class
		{
			if (string.IsNullOrEmpty(key))
				return null;
			var obj = AppCache.Get(key) as TValue;
			if (string.IsNullOrEmpty(partitionName))
				return obj;
			var indexList = AppCache.Get(ComposePartitionKey(partitionName)) as List<string> ?? new List<string>();
			if (indexList.Count <= 0)
				return obj;
			if (obj != null && indexList.All(reg => reg != key))
				indexList.Add(key);
			if (obj == null && indexList.Any(reg => reg == key))
				indexList.RemoveAll(reg => reg == key);
			AppCache.Insert(ComposePartitionKey(partitionName), indexList, null, Cache.NoAbsoluteExpiration, _indexSlidingExpiration);
			return obj;
		}

		public async Task<TValue> GetValueAsync<TValue>(string key, string partitionName = "") where TValue : class
		{
			return await Task.Run(() => GetValue<TValue>(key, partitionName)).ConfigureAwait(false);
		}

		public Task<List<string>> GetAllPartitionNamesAsync() { return null; }

		public List<TValue> Find<TValue>(string indexName, string indexValue, string partitionName = "") where TValue : class
		{
			return FindAsync<TValue>(indexName, indexValue, partitionName).Result;
		}

		public async Task<List<TValue>> FindAsync<TValue>(string indexName, string indexValue, string partitionName = "") where TValue : class
		{
			if (string.IsNullOrEmpty(indexName) || string.IsNullOrEmpty(indexValue))
				return new List<TValue>();
			var indexKey = ComposeKeyForCustomIndex(indexName, indexValue);
			var indexValues = await GetAllItemsFromSetAsync<TValue>(indexKey).ConfigureAwait(false);
			return indexValues;
		}

		public List<TValue> GetAllItemsInPartition<TValue>(string partitionName) where TValue : class
		{
			if (string.IsNullOrEmpty(partitionName))
				return new List<TValue>();
			var partitionKey = ComposePartitionKey(partitionName);
			var itemsInCache = GetAllItemsFromSetAsync<TValue>(partitionKey).Result;
			return itemsInCache;
		}

		public async Task<List<TValue>> GetAllItemsInPartitionAsync<TValue>(string partitionName) where TValue : class
		{
			var trackedItems = GetAllItemsInPartition<TValue>(partitionName).ToList();
			return await Task.Run(() => trackedItems).ConfigureAwait(false);
		}

		public void AddOrUpdate(string key, object value, TimeSpan? timeout, string partitionName = "")
		{
			if (timeout == null)
				AppCache.Insert(key, value, null, Cache.NoAbsoluteExpiration, _indexSlidingExpiration);
			else
				AppCache.Insert(key, value, null, Cache.NoAbsoluteExpiration, (TimeSpan)timeout);
			if (string.IsNullOrEmpty(partitionName))
				return;
			var partitionKey = ComposePartitionKey(partitionName);
			AddOrUpdatePartitionNamesList(partitionKey);
			AppCache.Insert(AllPartitionsCacheKey, partitionKey);
			var indexList = AppCache.Get(partitionKey) as List<string> ?? new List<string>();
			if (indexList.All(reg => reg != key))
				indexList.Add(key);
			AppCache.Insert(partitionKey, indexList, null, Cache.NoAbsoluteExpiration, _indexSlidingExpiration);
		}

		public async Task AddOrUpdateAsync(string key, object value, TimeSpan? timeout, string partitionName = "")
		{
			await Task.Run(() => AddOrUpdate(key, value, timeout, partitionName)).ConfigureAwait(false);
		}

		public void AddOrUpdateItemOnCustomIndex(string indexName, string indexedValue, string indexedObjectCacheKey, string partitionName = "")
		{
			var indexCacheKey = ComposeKeyForCustomIndex(indexName, indexedValue);
			var customIndexValueCacheKeyList = GetValue<List<String>>(indexCacheKey) ?? new List<string>();
			customIndexValueCacheKeyList.RemoveAll(val => val == indexedObjectCacheKey);
			customIndexValueCacheKeyList.Add(indexedObjectCacheKey);
			AppCache.Insert(indexCacheKey, customIndexValueCacheKeyList, null, Cache.NoAbsoluteExpiration, _indexSlidingExpiration);
		}

		public async Task AddOrUpdateItemOnCustomIndexAsync(string indexName, string indexedValue, string indexedObjectCacheKey, string partitionName = "")
		{
			await Task.Run(() => AddOrUpdateItemOnCustomIndex(indexName, indexedValue, indexedObjectCacheKey)).ConfigureAwait(false);
		}

		public void Remove(string key, string partitionName = "")
		{
			if (string.IsNullOrEmpty(key))
				return;
			AppCache.Remove(key);
			if (string.IsNullOrEmpty(partitionName))
				return;
			var partitionList = AppCache.Get(ComposePartitionKey(partitionName)) as List<string> ?? new List<string>();
			if (partitionList.Count <= 0)
				return;
			partitionList.RemoveAll(reg => reg == key);
			AppCache.Insert(ComposePartitionKey(partitionName), partitionList, null, Cache.NoAbsoluteExpiration, _indexSlidingExpiration);
		}

		public async Task RemoveAsync(string key, string partitionName = "")
		{
			await Task.Run(() => Remove(key, partitionName)).ConfigureAwait(false);
		}

		public void RemoveFromCustomIndex(string indexName, string indexedValue, string cacheKey, string partitionName = "")
		{
			var indexCacheKey = ComposeKeyForCustomIndex(indexName, indexedValue);
			var customIndex = AppCache.Get(indexCacheKey) as List<string> ?? new List<string>();
			if (customIndex.Count < 1)
				return;
			customIndex.RemoveAll(item => item == cacheKey);
			AppCache.Insert(indexCacheKey, customIndex, null, Cache.NoAbsoluteExpiration, _indexSlidingExpiration);
		}

		public async Task RemoveFromCustomIndexAsync(string indexName, string indexedValue, string cacheKey, string partitionName = "")
		{
			await Task.Run(() => RemoveFromCustomIndex(indexName, indexedValue, "")).ConfigureAwait(false);
		}

		public async Task RemoveExpiredItemsFromPartitionAsync(string partitionName)
		{
			// Not needed on HttpRuntime Cache
		}

		public void ClearCache()
		{
			var enumerator = AppCache.GetEnumerator();
			var cacheItems = new List<string>();
			while (enumerator.MoveNext())
			{
				cacheItems.Add(enumerator.Key.ToString());
			}
			foreach (var key in cacheItems)
			{
				AppCache.Remove(key);
			}
		}

		public async Task ClearCacheAsync() { await Task.Run(() => ClearCache()).ConfigureAwait(false); }

		public string ComposeKeyForCustomIndex(string indexName, string indexValue)
		{
			if (string.IsNullOrEmpty(indexName) || string.IsNullOrEmpty(indexValue))
				return null;
			var hashedIndexValue = ComputeBasicHash(indexValue);
			return string.Format("{0}_{1}_{2}", "Custom_Index_For", indexName, hashedIndexValue);
		}

		private async Task<List<TValue>> GetAllItemsFromSetAsync<TValue>(string setCacheKey) where TValue : class
		{
			if (string.IsNullOrEmpty(setCacheKey))
				return new List<TValue>();
			var setMembers = await GetValueAsync<List<string>>(setCacheKey).ConfigureAwait(false);
			if (setMembers == null || setMembers.Count < 1)
				return new List<TValue>();
			var indexedObjects = new List<TValue>();
			var keysToRemove = new List<string>();

			// Iterate through the index list
			foreach (var keyItem in setMembers)
			{
				var cacheItem = AppCache.Get(keyItem) as TValue;

				// If the item is null it means we have a stale key in the index and need to remove it (typically from when a cache item naturally expires). 
				// We will add it to the "keysToRemove" list to go through after this foreach statement.
				if (cacheItem == null)
					keysToRemove.Add(keyItem);
				else // We simply add the cacheItem to the list of indexedValues that we will eventually return.
					indexedObjects.Add(cacheItem);
			}

			// Remove any keys that are stale
			if (keysToRemove.Count <= 0)
				return indexedObjects;
			keysToRemove.ForEach(ktr => setMembers.RemoveAll(item => item == ktr));
			AppCache.Insert(setCacheKey, setMembers, null, Cache.NoAbsoluteExpiration, _indexSlidingExpiration);

			// return the retrieved values
			return indexedObjects;
		}

		private void AddOrUpdatePartitionNamesList(string partitionKey)
		{
			var partitionNames = GetValue<List<string>>(AllPartitionsCacheKey) ?? new List<string>();
			if (partitionNames.Contains(partitionKey))
				return;
			partitionNames.Add(partitionKey);
			AppCache.Insert(AllPartitionsCacheKey, partitionNames);
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

		private static string BytesToHexString(byte[] byteArray) { return byteArray == null ? null : new SoapHexBinary(byteArray).ToString(); }

		private static string ComposePartitionKey(string partitionName) { return string.Format("{0}_{1}", "PartitionKey_", partitionName); }
	}
}
﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RedisRepo.Src
{
	/// <summary>
	/// Represents the most common methods for interacting with the application's underlying cache provider.
	/// </summary>
	public interface IAppCache
	{
		/// <summary>
		/// Determines whether an item with the given cache key is in the cache.
		/// </summary>
		/// <param name="key">Cache Key</param>
		/// <param name="partitionName">
		/// Optional value for the name of a partition in the application cache. Partitions can be a good way to categorize or group
		/// certain types of items in the cache together.
		/// </param>
		/// <returns>Whether or not there is an item in the cache with that key and partition name</returns>
		bool Contains(string key, string partitionName = "");
        /// <summary>
		/// Determines whether an item with the given cache key is in the cache.
		/// </summary>
		/// <param name="key">Cache Key</param>
		/// <param name="partitionName">
		/// Optional value for the name of a partition in the application cache. Partitions can be a good way to categorize or group
		/// certain types of items in the cache together.
		/// </param>
		/// <returns>Whether or not there is an item in the cache with that key and partition name</returns>
		Task<bool> ContainsAsync(string key, string partitionName = "");

        /// <summary>
        /// Gets a regular Object out of the cache.
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <param name="partitionName">
        /// Optional value for the name of a partition in the application cache. Partitions can be a good way to categorize or group
		/// certain types of items in the cache together.
        /// </param>
        /// <returns>Regular (untyped) C# object representing the value in the cache</returns>
		object Get(string key, string partitionName = "");
        /// <summary>
        /// Gets a regular Object out of the cache.
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <param name="partitionName">
        /// Optional value for the name of a partition in the application cache. Partitions can be a good way to categorize or group
		/// certain types of items in the cache together.
        /// </param>
        /// <returns>Regular (untyped) C# object representing the value in the cache</returns>
		Task<object> GetAsync(string key, string partitionName = "");

        /// <summary>
        /// Gets the strongly typed value out of the cache based on the given key (and optional partition name)
        /// </summary>
        /// <typeparam name="TValue">Strong type representing the return value</typeparam>
        /// <param name="key">Cache Key</param>
        /// <param name="partitionName">
        /// Optional value for the name of a partition in the application cache. Partitions can be a good way to categorize or group
		/// certain types of items in the cache together.
        /// </param>
        /// <returns>Value in the cache</returns>
		TValue GetValue<TValue>(string key, string partitionName = "") where TValue : class;
        /// <summary>
        /// Gets the strongly typed value out of the cache based on the given key (and optional partition name)
        /// </summary>
        /// <typeparam name="TValue">Strong type representing the return value</typeparam>
        /// <param name="key">Cache Key</param>
        /// <param name="partitionName">
        /// Optional value for the name of a partition in the application cache. Partitions can be a good way to categorize or group
		/// certain types of items in the cache together.
        /// </param>
        /// <returns>Value in the cache</returns>
		Task<TValue> GetValueAsync<TValue>(string key, string partitionName = "") where TValue : class;

        /// <summary>
        /// Gets all the partition names that have been entered into the cache. Each time a partition is given to be used, it is 
        /// saved in the cache for tracking purposes.
        /// </summary>
        /// <returns>List of all partition names</returns>
		Task<List<string>> GetAllPartitionNamesAsync();

        /// <summary>
        /// Gets all the items in a given partition.
        /// </summary>
        /// <typeparam name="TValue">Type of the return value</typeparam>
        /// <param name="partitionName">
        /// Optional value for the name of a partition in the application cache. Partitions can be a good way to categorize or group
		/// certain types of items in the cache together.
        /// </param>
        /// <returns>List of all items in a partition</returns>
		List<TValue> GetAllItemsInPartition<TValue>(string partitionName) where TValue : class;
        /// <summary>
        /// Gets all the items in a given partition.
        /// </summary>
        /// <typeparam name="TValue">Type of the return value</typeparam>
        /// <param name="partitionName">
        /// Optional value for the name of a partition in the application cache. Partitions can be a good way to categorize or group
		/// certain types of items in the cache together.
        /// </param>
        /// <returns>List of all items in a partition</returns>
		Task<List<TValue>> GetAllItemsInPartitionAsync<TValue>(string partitionName) where TValue : class;

        /// <summary>
        /// Finds objects that are in a given index.
        /// </summary>
        /// <typeparam name="TValue">Type of return value</typeparam>
        /// <param name="indexName">Name of the index under which this object should be found</param>
        /// <param name="indexValue">The value on which to search</param>
        /// <param name="partitionName">
        /// Optional value for the name of a partition in the application cache. Partitions can be a good way to categorize or group
		/// certain types of items in the cache together.
        /// </param>
        /// <returns>List of all values in an index.</returns>
		List<TValue> Find<TValue>(string indexName, string indexValue, string partitionName = "") where TValue : class;
        /// <summary>
        /// Finds objects that are in a given index.
        /// </summary>
        /// <typeparam name="TValue">Type of return value</typeparam>
        /// <param name="indexName">Name of the index under which this object should be found</param>
        /// <param name="indexValue">The value on which to search</param>
        /// <param name="partitionName">
        /// Optional value for the name of a partition in the application cache. Partitions can be a good way to categorize or group
		/// certain types of items in the cache together.
        /// </param>
        /// <returns>List of all values in an index.</returns>
		Task<List<TValue>> FindAsync<TValue>(string indexName, string indexValue, string partitionName = "") where TValue : class;

        /// <summary>
        /// Inserts or updates the value in the cache.
        /// </summary>
        /// <param name="key">Cache Key</param>
        /// <param name="value">Value to add or update</param>
        /// <param name="timeout">Timespan of time to live in the cache. If null, then it will stay in the cache indefinitely.</param>
        /// <param name="partitionName">
        /// Optional value for the name of a partition in the application cache. Partitions can be a good way to categorize or group
		/// certain types of items in the cache together.
        /// </param>
		void AddOrUpdate(string key, object value, TimeSpan? timeout, string partitionName = "");
        /// <summary>
        /// Inserts or updates the value in the cache.
        /// </summary>
        /// <param name="key">Cache Key</param>
        /// <param name="value">Value to add or update</param>
        /// <param name="timeout">Timespan of time to live in the cache. If null, then it will stay in the cache indefinitely.</param>
        /// <param name="partitionName">
        /// Optional value for the name of a partition in the application cache. Partitions can be a good way to categorize or group
		/// certain types of items in the cache together.
        /// </param>
		Task AddOrUpdateAsync(string key, object value, TimeSpan? timeout, string partitionName = "");

        /// <summary>
        /// Adds or updates a value on an index.
        /// </summary>
        /// <param name="indexName">Name of the index</param>
        /// <param name="indexedValue">Value to be indexed</param>
        /// <param name="indexedObjectCacheKey">
        /// This is the cache key for the actual object as it is stored in the cache. These indexes are stored as bookmark lookups so we store 
        /// the indexed value for search and then the indexed object's cache key to get the actual object out of cache.
        /// </param>
        /// <param name="partitionName">
        /// Optional value for the name of a partition in the application cache. Partitions can be a good way to categorize or group
		/// certain types of items in the cache together.</param>
		void AddOrUpdateItemOnCustomIndex(string indexName, string indexedValue, string indexedObjectCacheKey, string partitionName = "");
        /// <summary>
        /// Adds or updates a value on an index.
        /// </summary>
        /// <param name="indexName">Name of the index</param>
        /// <param name="indexedValue">Value to be indexed</param>
        /// <param name="indexedObjectCacheKey">
        /// This is the cache key for the actual object as it is stored in the cache. These indexes are stored as bookmark lookups so we store 
        /// the indexed value for search and then the indexed object's cache key to get the actual object out of cache.
        /// </param>
        /// <param name="partitionName">
        /// Optional value for the name of a partition in the application cache. Partitions can be a good way to categorize or group
		/// certain types of items in the cache together.
		/// </param>
		Task AddOrUpdateItemOnCustomIndexAsync(string indexName, string indexedValue, string indexedObjectCacheKey, string partitionName = "");

        /// <summary>
        /// Removes an item from the cache
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <param name="partitionName">
        /// Optional value for the name of a partition in the application cache. Partitions can be a good way to categorize or group
		/// certain types of items in the cache together.
        /// </param>
		void Remove(string key, string partitionName = "");
        /// <summary>
        /// Removes an item from the cache
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <param name="partitionName">
        /// Optional value for the name of a partition in the application cache. Partitions can be a good way to categorize or group
		/// certain types of items in the cache together.
        /// </param>
		Task RemoveAsync(string key, string partitionName = "");

        /// <summary>
        /// Removes a value from a custom index
        /// </summary>
        /// <param name="indexName">Name of Index</param>
        /// <param name="indexedValue">Value to be removed from index</param>
        /// <param name="cacheKey">
        /// This is the cache key for the actual object as it is stored in the cache. These indexes are stored as bookmark lookups so we store 
        /// the indexed value for search and then the indexed object's cache key to get the actual object out of cache.
        /// </param>
        /// <param name="partitionName">
        /// Optional value for the name of a partition in the application cache. Partitions can be a good way to categorize or group
		/// certain types of items in the cache together.
		/// </param>
		void RemoveFromCustomIndex(string indexName, string indexedValue, string cacheKey, string partitionName = "");
        /// <summary>
        /// Removes a value from a custom index
        /// </summary>
        /// <param name="indexName">Name of Index</param>
        /// <param name="indexedValue">Value to be removed from index</param>
        /// <param name="cacheKey">
        /// This is the cache key for the actual object as it is stored in the cache. These indexes are stored as bookmark lookups so we store 
        /// the indexed value for search and then the indexed object's cache key to get the actual object out of cache.
        /// </param>
        /// <param name="partitionName">
        /// Optional value for the name of a partition in the application cache. Partitions can be a good way to categorize or group
		/// certain types of items in the cache together.
		/// </param>
		Task RemoveFromCustomIndexAsync(string indexName, string indexedValue, string cacheKey, string partitionName = "");

        /// <summary>
        /// Removes all items from a partition whose custom timeout has expired.
        /// </summary>
        /// <param name="partitionName">
        /// The name of a partition in the application cache. Partitions can be a good way to categorize or group
		/// certain types of items in the cache together.
        /// </param>
        /// <returns>Void</returns>
		Task RemoveExpiredItemsFromPartitionAsync(string partitionName);

        /// <summary>
        /// Clears out all Items in a cache. For Redis, it only clears out items that belong to the particular database inside the redis server
        /// based on the connection string of the current connection.
        /// </summary>
		void ClearCache();
        /// <summary>
        /// Clears out all Items in a cache. For Redis, it only clears out items that belong to the particular database inside the redis server
        /// based on the connection string of the current connection.
        /// </summary>
		Task ClearCacheAsync();

        /// <summary>
        /// Composes a delimited name for an index in order for it to be easily grouped in a view of the cache.
        /// </summary>
        /// <param name="indexName">Name of the Index</param>
        /// <param name="indexValue">Value of the index</param>
        /// <returns>A fully configured name for an index</returns>
        string ComposeKeyForCustomIndex(string indexName, string indexValue);
	}
}
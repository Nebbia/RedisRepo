using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace RedisRepo.Src
{
	public class CacheRepo<T> : ICacheRepo<T> where T : class
	{
		private Func<T, string> _primaryCacheKey;
		private List<Func<T, KeyValuePair<string, string>>> _customIndices;
		private readonly IAppCache _appCache;

		public CacheRepo(IAppCache appCache) { _appCache = appCache; }

		public virtual Func<T, string> PrimaryEntityId
		{
			get { return _primaryCacheKey ?? (_primaryCacheKey = GetPrimaryEntityId); }
			set { _primaryCacheKey = value; }
		}

		public virtual List<Func<T, KeyValuePair<string, string>>> CustomIndices
		{
			get { return _customIndices ?? (_customIndices = new List<Func<T, KeyValuePair<string, string>>>()); }
			set { _customIndices = value; }
		}

		public virtual bool CustomIndicesAreSet { get; set; }

		public virtual void SetCustomCacheIndices(T entity) { }

		public virtual IAppCache AppCache { get { return _appCache; } }

		public virtual async Task AddOrUpdateAsync(T entity)
		{
			if (!CustomIndicesAreSet)
			{
				SetCustomCacheIndices(entity);
				CustomIndicesAreSet = true;
			}
			var primaryEntityId = PrimaryEntityId(entity);
			var primaryCacheKey = ComposePrimaryCacheKey(primaryEntityId);
			var current = await GetAsync(primaryEntityId).ConfigureAwait(false);
			if (current != null)
				await RemoveFromCacheAsync(current).ConfigureAwait(false);
			await _appCache.AddOrUpdateAsync(primaryCacheKey, entity, null, ComposePartitionName()).ConfigureAwait(false);
			await AddOrUpdateCustomIndicesAsync(entity).ConfigureAwait(false);
		}

		public virtual async Task RemoveFromCacheAsync(T entity)
		{
			if (!CustomIndicesAreSet)
			{
				SetCustomCacheIndices(entity);
				CustomIndicesAreSet = true;
			}
			var primaryEntityId = PrimaryEntityId(entity);
			var primaryCacheKey = ComposePrimaryCacheKey(primaryEntityId);
			await _appCache.RemoveAsync(primaryCacheKey, ComposePartitionName()).ConfigureAwait(false);
			await RemoveCustomIndicesAsync(entity).ConfigureAwait(false);
		}

		public virtual async Task<T> GetAsync(string id)
		{
			var primaryCacheKey = ComposePrimaryCacheKey(id);
			var response = await _appCache.GetValueAsync<T>(primaryCacheKey, ComposePartitionName()).ConfigureAwait(false);
			return response;
		}

		public virtual async Task<List<T>> GetAllAsync(bool skipDatabaseConsistencyCheck = false)
		{
			// If this is the first time the GetAllAsync has been called, we return 0, despite the fact that there might be some in the cache.
			// This is because we want to force the call to the database to ensure that we have a true "all" which will also then populate the cache.
			if (skipDatabaseConsistencyCheck)
			{
				var allItemsNonDbCheck = await _appCache.GetAllItemsInPartitionAsync<T>(ComposePartitionName()).ConfigureAwait(false);
				return allItemsNonDbCheck;
			}
			var lastCallToGetAllKey = ComposeLastCallToGetAllCacheKey();
			var containsKey = await _appCache.ContainsAsync(lastCallToGetAllKey).ConfigureAwait(false);
			if (containsKey)
			{
				await _appCache.AddOrUpdateAsync(lastCallToGetAllKey, DateTimeOffset.UtcNow, TimeSpan.FromDays(2)).ConfigureAwait(false);
				var response = await _appCache.GetAllItemsInPartitionAsync<T>(ComposePartitionName()).ConfigureAwait(false);
				return response;
			}
			await _appCache.AddOrUpdateAsync(lastCallToGetAllKey, DateTimeOffset.UtcNow, TimeSpan.FromDays(2)).ConfigureAwait(false);
			return new List<T>();
		}

		public virtual async Task<List<T>> GetAllWhereAsync(Func<T, bool> filterPredicate, bool skipDatabaseConsistencyCheck = false)
		{
			if (filterPredicate == null)
				return new List<T>();

			// If this is the first time the GetAllAsync has been called, we return 0, despite the fact that there might be some in the cache.
			// This is because we want to force the call to the database to ensure that we have a true "all" which will also then populate the cache
			// in the service layer.
			if (skipDatabaseConsistencyCheck)
			{
				var allItemsNoDbCheck = await _appCache.GetAllItemsInPartitionAsync<T>(ComposePartitionName()).ConfigureAwait(false);
				return allItemsNoDbCheck.Where(filterPredicate).ToList();
			}
			var key = ComposeLastCallToGetAllCacheKey();
			var containsKey = _appCache.Contains(key);
			if (!containsKey)
			{
				_appCache.AddOrUpdate(key, DateTimeOffset.UtcNow, TimeSpan.FromDays(2));
				return new List<T>();
			}
			_appCache.AddOrUpdate(key, DateTimeOffset.UtcNow, TimeSpan.FromDays(2));
			var allItems = await _appCache.GetAllItemsInPartitionAsync<T>(ComposePartitionName()).ConfigureAwait(false);
			return allItems.Where(filterPredicate).ToList();
		}

		public virtual async Task<List<T>> FindAsync(string indexName, string indexedValue)
		{
			var results = await _appCache.FindAsync<T>(indexName, indexedValue, ComposePartitionName()).ConfigureAwait(false);
			return results;
		}

		public virtual string ComposePartitionName() { return string.Format("PartitionNameFor:{0}:{1}", typeof(T).Name, typeof(T).Namespace); }

		public virtual string ComposePrimaryCacheKey(string entityId) { return string.Format("PrimaryEntityFor:{0}:{1}", typeof(T).Name, entityId); }

		/// <summary>
		///     Default implmentation of the PrimaryEntityId Func
		/// </summary>
		/// <param name="entity">Entity from which to get the string value of its ID property</param>
		/// <returns>string version of the entity's Id property</returns>
		public virtual string GetPrimaryEntityId(T entity)
		{
			var entityPropInfos = typeof(T).GetProperties().ToList();
			PropertyInfo propInfo;
			string idPropValue;
			if (entityPropInfos.Any(pi => string.Equals(pi.Name, "Id", StringComparison.InvariantCultureIgnoreCase)))
			{
				propInfo = entityPropInfos.FirstOrDefault(pi => string.Equals(pi.Name, "Id", StringComparison.InvariantCultureIgnoreCase));
				if (propInfo != null)
				{
					idPropValue = propInfo.GetValue(entity).ToString();
					return idPropValue;
				}
			}
			var typeNameId = string.Format("{0}Id", typeof(T).Name);
			if (entityPropInfos.Any(pi => string.Equals(pi.Name, typeNameId, StringComparison.InvariantCultureIgnoreCase)))
			{
				propInfo = entityPropInfos.FirstOrDefault(pi => string.Equals(pi.Name, typeNameId, StringComparison.InvariantCultureIgnoreCase));
				if (propInfo != null)
				{
					idPropValue = propInfo.GetValue(entity).ToString();
					return idPropValue;
				}
			}
			if (entityPropInfos.Any(pi => string.Equals(pi.Name, "EntityId", StringComparison.InvariantCultureIgnoreCase)))
			{
				propInfo = entityPropInfos.FirstOrDefault(pi => string.Equals(pi.Name, "EntityId", StringComparison.InvariantCultureIgnoreCase));
				if (propInfo != null)
				{
					idPropValue = propInfo.GetValue(entity).ToString();
					return idPropValue;
				}
			}
			throw new Exception("There was no Id property found on the given entity for Redis caching.");
		}

		private async Task AddOrUpdateCustomIndicesAsync(T entity)
		{
			if (CustomIndices.Count < 1)
				return;
			foreach (var kvp in CustomIndices.Select(customIndex => customIndex(entity)))
			{
				var primaryEntityId = PrimaryEntityId(entity);
				var primaryCacheKey = ComposePrimaryCacheKey(primaryEntityId);
				if (string.IsNullOrEmpty(kvp.Key) || string.IsNullOrEmpty(kvp.Value))
					continue;
				await _appCache.AddOrUpdateItemOnCustomIndexAsync(kvp.Key, kvp.Value, primaryCacheKey, ComposePartitionName()).ConfigureAwait(false);
			}
		}

		private async Task RemoveCustomIndicesAsync(T entity)
		{
			if (CustomIndices.Count < 1)
				return;
			foreach (var kvp in CustomIndices.Select(customIndex => customIndex(entity)))
			{
				var primaryEntityId = PrimaryEntityId(entity);
				var primaryCacheKey = ComposePrimaryCacheKey(primaryEntityId);
				await _appCache.RemoveFromCustomIndexAsync(kvp.Key, kvp.Value, primaryCacheKey, ComposePartitionName()).ConfigureAwait(false);
			}
		}

		private static string ComposeLastCallToGetAllCacheKey() { return string.Format("LastToGetAllFor:{0}", typeof(T).Name); }
	}
}
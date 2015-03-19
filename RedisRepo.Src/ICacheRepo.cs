using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RedisRepo.Src
{
	/// <summary>
	/// 
	/// </summary>
	public interface ICacheRepo<T> where T : class
	{
		/// <summary>
		///     Function that takes in the object and returns the value of the primary Id of that object. The default
		///     implementation of this reflects over the object and looks for properties that are named Id, [Type-Name]Id,
		///     or EntityId (in that order). If it doesn't find anything like that an exception is thrown.
		/// </summary>
		Func<T, string> PrimaryEntityId { get; set; }

		/// <summary>
		///     <para>
		///         List of custom indices on the entity being saved as defined by the given Funcs.
		///     </para>
		///     <para>
		///         The string 'key' for the resulting KeyValuePair is the index name and the 'value' is
		///         the indexed value to search by. The indexed value will only be searched using a string
		///         "Equals".
		///     </para>
		///     <para>
		///         As an aside, each custom index is a list of name-value pairs, where the 'name' is the
		///         searchable content, and the 'value' is the cache key (bookmark lookup) of the object to
		///         be returned.
		///     </para>
		/// </summary>
		List<Func<T, KeyValuePair<string, string>>> CustomIndices { get; set; }

		/// <summary>
		///		Defines whether the custom Indices have been set for the current instance of this ICacheRepo.
		/// </summary>
		bool CustomIndicesAreSet { get; set; }

		/// <summary>
		///		Method used to set the cache indices on this ICacheRepo instance.
		/// </summary>
		/// <param name="entity"></param>
		void SetCustomCacheIndices(T entity);

		/// <summary>
		///     Reference to the current implementation of IAppCache.
		/// </summary>
		IAppCache AppCache { get; }

		/// <summary>
		///     Does just what the method name says. Adds or Updates a cache item. This includes all the indices that
		///     are defined in the CustomIndices property.
		/// </summary>
		Task AddOrUpdateAsync(T entity);

		/// <summary>
		///     Removes an item from cache, including the custom indices defined in the CustomIndices property.
		/// </summary>
		Task RemoveFromCacheAsync(T entity);

		/// <summary>
		///     Gets the object based on the given ID. A string is asked for since the PrimaryEntityId function property
		///     defines that the Id property will be converted and returned as a string. This makes the use case of
		///     this method more flexible.
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		Task<T> GetAsync(string id);

		/// <summary>
		///     <para>
		///         Returns all the items that are available in the cache for this type of object.
		///     </para>
		///     <para>
		///         By default, it checks
		///         to see if we have retrieved all items from the database. If not, we return an empty list despite
		///         the fact that we might actually have items in the cache. This will force the caller to go to the
		///         database and then populate the cache (assuming the use of a Cache-Aside pattern).
		///     </para>
		///     <para>
		///         This logic ensures that we stay as consistent with the database as possible.
		///     </para>
		/// </summary>
		/// <param name="skipDatabaseConsistencyCheck">Whether or not to skip the database consistency check</param>
		Task<List<T>> GetAllAsync(bool skipDatabaseConsistencyCheck = false);

		/// <summary>
		///     <para>
		///         Returns all the items that are available in the cache for this type of object filtered by the
		///         given predicate expression. This method will load all items into an in-memory List first and
		///         then run the filter expression.
		///     </para>
		///     <para>
		///         By default, it checks
		///         to see if we have retrieved all items from the database. If not, we return an empty list despite
		///         the fact that we might actually have items in the cache. This will force the caller to go to the
		///         database and then populate the cache (assuming the use of a Cache-Aside pattern).
		///     </para>
		///     <para>
		///         This logic ensures that we stay as consistent with the database as possible.
		///     </para>
		/// </summary>
		/// <param name="filterPredicate">Expression for filtering data</param>
		/// <param name="skipDatabaseConsistencyCheck">Whether or not to skip the database consistency check</param>
		Task<List<T>> GetAllWhereAsync(Func<T, bool> filterPredicate, bool skipDatabaseConsistencyCheck = false);

		/// <summary>
		///     Finds a cached object based on a pre-defined index that is named the same as the given indexName parameter.
		/// </summary>
		/// <param name="indexName">Name of predefined index name</param>
		/// <param name="indexedValue">Value to search by. The search is an equivalent search.</param>
		Task<List<T>> FindAsync(string indexName, string indexedValue);

		/// <summary>
		///     Composes the default tracking index key. The Tracking index is what is used to keep track of all items of a
		///     particular type that are in the cache.
		/// </summary>
		/// <returns></returns>
		string ComposePartitionName();

		/// <summary>
		///     Composes the cache key for the object. This is based on the objects type name as well as the value of its
		///     id property.
		/// </summary>
		/// <param name="entityId"></param>
		/// <returns></returns>
		string ComposePrimaryCacheKey(string entityId);
	}
}
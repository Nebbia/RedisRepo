﻿using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace RedisRepo.Src
{
	public interface IRedisHash<T> where T : class
	{
		bool FlattenCollections { get; set; }

		/// <summary>
		///     <para>
		///         Generates the redis key for the given type based on the return value of the Func property.
		///     </para>
		///     <para>
		///         The
		///         default implmentation looks like "ObjectHash:[ValueOfSomeIdProperty]" where the right side of
		///         the key is the result of reflecting over the object looking for a property that has a name of "Id",
		///         "[ClassTypeName].Id", or "EntityId".
		///     </para>
		///     <para>
		///			If no property is found an exception is thrown.
		///     </para>
		/// </summary>
		Func<T, string> RedisKeyGenerator { get; set; }

		/// <summary>
		///		<para>
		///			Takes the given entity and converts its public properties into a redis hash and saves it to redis.
		///		</para>
		///		<para>
		///			The key is generated by calling the <see cref="RedisKeyGenerator"/> Func property.
		///		</para>
		/// </summary>
		/// <param name="entity"></param>
		/// <returns></returns>
		Task SetAllAsync(T entity);

		///  <summary>
		/// 	Gets a redis hash at the given key and deserializes it into a type of <see cref="T"/>
		///  </summary>
		Task<T> GetAllAsync(string redisKey);

		/// <summary>
		///		Deletes the redis hash at the key equivalent to what the <see cref="RedisKeyGenerator"/> produces.
		/// </summary>
		/// <param name="entity"></param>
		Task DeleteAsync(T entity);

		/// <summary>
		///		Gets a strongly typed hash value based on the evaluation of the <see cref="propertyExpression"/>. The 
		///		redis key is produced by the <see cref="RedisKeyGenerator"/>.
		/// </summary>
		/// <param name="propertyExpression">Func expression that gets a property name.</param>
		Task<THashValue> GetAsync<THashValue>(Expression<Func<T, object>> propertyExpression);

		/// <summary>
		///		Sets a redis hash value at the hash name that is the same name as the given property name
		///		as a result of evaluating the <see cref="propertyExpression"/>.
		/// </summary>
		/// <param name="propertyExpression"></param>
		/// <returns></returns>
		Task SetAsync(Expression<Func<T, object>> propertyExpression);

		/// <summary>
		/// Checks if the hash exists.
		/// </summary>
		/// <returns></returns>
		Task<bool> ExistsAsync(T entity);
	}
}
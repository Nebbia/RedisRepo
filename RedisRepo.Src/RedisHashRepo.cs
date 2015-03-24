// Copyright 2012-2014 Unikey Technologies, Inc. All Rights Reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using StackExchange.Redis;

namespace RedisRepo.Src
{
	public class RedisHash<T> : IRedisHash<T> where T : class, new()
	{
		private Func<T, string> _primaryEntityId;
		private Func<string, string> _primaryCacheKeyFormatter;
		private Func<string, string, string> _collectionHashFieldNameFormatter;

		public RedisHash(RedisConfig redisConfig)
		{
			RedisDatabase = redisConfig.RedisMultiplexer.GetDatabase(redisConfig.RedisDatabaseId);
			FlattenDictionaries = false;
		}

		public bool FlattenDictionaries { get; set; }

		public Func<T, string> PrimaryEntityIdLocator
		{
			get { return _primaryEntityId ?? (_primaryEntityId = GetDefaultEntityId); }
			set { _primaryEntityId = value; }
		}

		public Func<string, string> PrimaryCacheKeyFormatter
		{
			get { return _primaryCacheKeyFormatter ?? (_primaryCacheKeyFormatter = s => s); }
			set { _primaryCacheKeyFormatter = value; }
		}

		public IDatabase RedisDatabase { get; private set; }

		public Func<string, string, string> DictionaryHashFieldNameFormatter
		{
			get
			{
				return _collectionHashFieldNameFormatter ??
				       (_collectionHashFieldNameFormatter = (propertyName, fieldName) => string.Format("{0}:{1}", propertyName, fieldName));
			}
			set { _collectionHashFieldNameFormatter = value; }
		}

		public async Task SetAllAsync(T entity)
		{
			var hashEntries = new List<HashEntry>();
			var props = typeof(T).GetProperties().ToList();
			foreach(var propertyInfo in props)
			{
				var hashFieldName = propertyInfo.Name;
				var propInterfaces = propertyInfo.PropertyType.GetInterfaces();
				if(FlattenDictionaries && CanGetHashItemsFromCollection(propertyInfo, propInterfaces, hashFieldName, hashEntries, entity))
					continue;
				var itemFieldValue = JsonConvert.SerializeObject(propertyInfo.GetValue(entity));
				var hashEntry = new HashEntry(hashFieldName, itemFieldValue);
				hashEntries.Add(hashEntry);
			}
			await RedisDatabase.HashSetAsync(PrimaryCacheKeyFormatter(PrimaryEntityIdLocator(entity)), hashEntries.ToArray()).ConfigureAwait(false);
		}

		public async Task<T> GetAllAsync(string entityId)
		{
			var redisKey = PrimaryCacheKeyFormatter(entityId);
			var hash = (await RedisDatabase.HashGetAllAsync(redisKey).ConfigureAwait(false)).ToList();
			var props = typeof(T).GetProperties();
			var entity = new T();
			foreach(var prop in props)
			{
				var propInterfaces = prop.PropertyType.GetInterfaces();

				// If it's a collection property, extract out the values from the hash
				if(CanPutHashItemsIntoCollection(prop, propInterfaces, hash, entity))
					continue;

				// Else try to get a hash value for the given property.
				var hashItem = hash.FirstOrDefault(h => h.Name == prop.Name);
				if(hashItem == default(HashEntry) || hashItem.Value.IsNull)
					continue;

				// Deserialize the value
				var val = JsonConvert.DeserializeObject(hashItem.Value, prop.PropertyType);
				prop.SetValue(entity, val);
			}
			return entity;
		}

		public async Task DeleteAsync(T entity)
		{
			var entityId = PrimaryEntityIdLocator(entity);
			await DeleteAsync(entityId).ConfigureAwait(false);
		}

		public async Task DeleteAsync(string entityId)
		{
			var redisKey = PrimaryCacheKeyFormatter(entityId);
			await RedisDatabase.KeyDeleteAsync(redisKey).ConfigureAwait(false);
		}

		public async Task<TFieldValue> GetFieldVaueAsync<TFieldValue>(string entityId, Expression<Func<T, TFieldValue>> propertyExpression)
		{
			var hashName = GetPropertyName(propertyExpression);
			return await GetFieldVaueAsync<TFieldValue>(entityId, hashName).ConfigureAwait(false);
		}

		public async Task<TFieldValue> GetDictionaryFieldValueAsync<TFieldValue>(string entityId, Expression<Func<T, object>> propertyExpression,
		                                                                         object dictionaryKey)
		{
			if(!FlattenDictionaries)
				return default(TFieldValue);
			var redisKey = PrimaryCacheKeyFormatter(entityId);
			var hashPropertyName = GetPropertyName(propertyExpression);
			var key = JsonConvert.SerializeObject(dictionaryKey);
			var hashName = DictionaryHashFieldNameFormatter(hashPropertyName, key);
			var hashVal = await RedisDatabase.HashGetAsync(redisKey, hashName).ConfigureAwait(false);
			var val = JsonConvert.DeserializeObject<TFieldValue>(hashVal);
			return val;
		}

		public async Task<TFieldValue> GetFieldVaueAsync<TFieldValue>(string entityId, string hashName)
		{
			var redisKey = PrimaryCacheKeyFormatter(entityId);
			var fieldType = typeof(TFieldValue);
			var isIDictionary = fieldType.GetInterfaces().Any(pi => pi.IsGenericType && pi.GetGenericTypeDefinition() == typeof(IDictionary<,>));
			if (isIDictionary && FlattenDictionaries)
			{
				var fullHash = await RedisDatabase.HashGetAllAsync(redisKey).ConfigureAwait(false);
				var dictionaryHashes = fullHash.Where(h => h.Name.ToString().Contains(hashName)).ToList();
				var constructedDictionary = GetConstructedDictionaryPropertyInstance(fieldType, dictionaryHashes, hashName);
				return (TFieldValue)constructedDictionary;
			}
			var hashVal = await RedisDatabase.HashGetAsync(redisKey, hashName).ConfigureAwait(false);
			var val = JsonConvert.DeserializeObject<TFieldValue>(hashVal);
			return val;
		}

		public async Task SetFieldValueAsync<TFieldValue>(string entityId, Expression<Func<T, TFieldValue>> propertyExpression, TFieldValue value)
		{
			var redisKey = PrimaryCacheKeyFormatter(entityId);
			var hashName = GetPropertyName(propertyExpression);
			var serializedObj = JsonConvert.SerializeObject(value);
			await RedisDatabase.HashSetAsync(redisKey, hashName, serializedObj).ConfigureAwait(false);
		}

		public async Task SetDictionaryFieldValueAsync<TFieldValue>(string entityId, Expression<Func<T, object>> propertyExpression,
		                                                            object dictionaryKey, TFieldValue value)
		{
			if(!FlattenDictionaries)
				return;
			var redisKey = PrimaryCacheKeyFormatter(entityId);
			var propertyName = GetPropertyName(propertyExpression);
			var serializedKey = JsonConvert.SerializeObject(dictionaryKey);
			var hashName = DictionaryHashFieldNameFormatter(propertyName, serializedKey);
			var serializedObj = JsonConvert.SerializeObject(value);
			await RedisDatabase.HashSetAsync(redisKey, hashName, serializedObj).ConfigureAwait(false);
		}

		public async Task<bool> ExistsAsync(T entity)
		{
			var entityId = PrimaryEntityIdLocator(entity);
			var redisKey = PrimaryCacheKeyFormatter(entityId);
			return await RedisDatabase.KeyExistsAsync(redisKey).ConfigureAwait(false);
		}

		public async Task<bool> ExistsAsync(string entityId)
		{
			var cacheKey = PrimaryCacheKeyFormatter(entityId);
			return await RedisDatabase.KeyExistsAsync(cacheKey).ConfigureAwait(false);
		}

		private static string GetDefaultEntityId(T entity)
		{
			var entityPropInfos = typeof(T).GetProperties().ToList();
			PropertyInfo propInfo;
			string idPropValue;
			if(entityPropInfos.Any(pi => string.Equals(pi.Name, "Id", StringComparison.InvariantCultureIgnoreCase)))
			{
				propInfo = entityPropInfos.FirstOrDefault(pi => string.Equals(pi.Name, "Id", StringComparison.InvariantCultureIgnoreCase));
				if(propInfo != null)
				{
					idPropValue = propInfo.GetValue(entity).ToString();
					return idPropValue;
				}
			}
			var typeNameId = string.Format("{0}Id", typeof(T).Name);
			if(entityPropInfos.Any(pi => string.Equals(pi.Name, typeNameId, StringComparison.InvariantCultureIgnoreCase)))
			{
				propInfo = entityPropInfos.FirstOrDefault(pi => string.Equals(pi.Name, typeNameId, StringComparison.InvariantCultureIgnoreCase));
				if(propInfo != null)
				{
					idPropValue = propInfo.GetValue(entity).ToString();
					return idPropValue;
				}
			}
			if(entityPropInfos.Any(pi => string.Equals(pi.Name, "EntityId", StringComparison.InvariantCultureIgnoreCase)))
			{
				propInfo = entityPropInfos.FirstOrDefault(pi => string.Equals(pi.Name, "EntityId", StringComparison.InvariantCultureIgnoreCase));
				if(propInfo != null)
				{
					idPropValue = propInfo.GetValue(entity).ToString();
					return idPropValue;
				}
			}
			throw new Exception("There was no Id property found on the given entity for Redis caching.");
		}

		private static string GetPropertyName<TFieldValue>(Expression<Func<T, TFieldValue>> propertyExpression)
		{
			var body = propertyExpression.Body;
			var convertExpression = body as UnaryExpression;
			if(convertExpression == null)
				return ((MemberExpression)body).Member.Name;
			if(convertExpression.NodeType != ExpressionType.Convert)
				throw new ArgumentException("Invalid property expression.", "propertyExpression");
			body = convertExpression.Operand;
			return ((MemberExpression)body).Member.Name;
		}

		private bool CanGetHashItemsFromCollection(PropertyInfo propertyInfo, Type[] propInterfaces, string hashFieldName, List<HashEntry> hashEntries,
		                                           T entity)
		{
			HashEntry hashEntry;
			var isIDictionary = propInterfaces.Any(pi => pi.IsGenericType && pi.GetGenericTypeDefinition() == typeof(IDictionary<,>));

			//if(propertyInfo.PropertyType.Name == "Dictionary`2")
			if(isIDictionary)
			{
				// We're not handling dictionaries right now until I can figure out a way to deserialize it back out of a redis hash
				//return false;
				foreach(var item in (IDictionary)propertyInfo.GetValue(entity))
				{
					var itemVal = (DictionaryEntry)item;
					var keySerialized = JsonConvert.SerializeObject(itemVal.Key);
					var valueSerialized = JsonConvert.SerializeObject(itemVal.Value);
					var itemFieldHashName = DictionaryHashFieldNameFormatter(hashFieldName, keySerialized);
					var itemFieldValue = valueSerialized;
					hashEntry = new HashEntry(itemFieldHashName, itemFieldValue);
					hashEntries.Add(hashEntry);
				}
				return true;
			}
			var isICollection = propInterfaces.Any(pi => pi.IsGenericType && pi.GetGenericTypeDefinition() == typeof(ICollection<>));
			var isArray = typeof(Array).IsAssignableFrom(propertyInfo.PropertyType);
			if(isICollection || isArray)
			{
				// We aren't going to flatten lists right now because getting and setting the individual items in a collection
				// will be fairly complex when doing it generically. Leaving it here because I spent a good long while trying to
				// figure out how to do this and don't want to forget how. I'll tag it in Git later and remove the code.
				return false;
				var index = 1;
				foreach(var listItem in (ICollection)propertyInfo.GetValue(entity))
				{
					var itemFieldHashName = DictionaryHashFieldNameFormatter(hashFieldName, index.ToString());
					var itemFieldValue = JsonConvert.SerializeObject(listItem);
					hashEntry = new HashEntry(itemFieldHashName, itemFieldValue);
					hashEntries.Add(hashEntry);
					index++;
				}
				return true;
			}
			return false;
		}

		private static bool CanPutHashItemsIntoCollection(PropertyInfo prop, Type[] propInterfaces, List<HashEntry> hash, T entity)
		{
			var hashEntries = new List<HashEntry>();
			var isIDictionary = propInterfaces.Any(pi => pi.IsGenericType && pi.GetGenericTypeDefinition() == typeof(IDictionary<,>));

			//if(prop.PropertyType.Name == "Dictionary`2")
			if(isIDictionary)
			{
				// Get all the hash entries for this property
				var filteredHashEnttries = hash.Where(h => h.Name.ToString().Contains(prop.Name)).ToList();
				if(filteredHashEnttries.Count < 1)
					return true;

				// See if there is just a single hash entry is 
				if(filteredHashEnttries.Count == 1)
				{
					var entry = filteredHashEnttries.FirstOrDefault();
					if(string.Equals(entry.Name.ToString(), prop.Name))
						return false;
				}
				hashEntries.AddRange(filteredHashEnttries);
				var constructedDictionary = GetConstructedDictionaryPropertyInstance(prop.PropertyType, hashEntries, prop.Name);
				prop.SetValue(entity, constructedDictionary);
				return true;
			}
			var isICollection = propInterfaces.Any(pi => pi.IsGenericType && pi.GetGenericTypeDefinition() == typeof(ICollection<>));
			var isArray = typeof(Array).IsAssignableFrom(prop.PropertyType);

			//if(prop.PropertyType.Name == "List`1")
			if(isICollection || isArray)
			{
				// We aren't going to flatten lists right now because getting and setting the individual items in a collection
				// will be fairly complex when doing it generically. Leaving it here because I spent a good long while trying to
				// figure out how to do this and don't want to forget how. I'll tag it in git later and remove the code.
				return false;
				var filteredHashEnttries = hash.Where(h => h.Name.ToString().Contains(prop.Name)).ToList();
				if(filteredHashEnttries.Count < 1)
					return true;

				// See if there is just a single hash entry
				if(filteredHashEnttries.Count == 1)
				{
					var entry = filteredHashEnttries.FirstOrDefault();
					if(string.Equals(entry.Name.ToString(), prop.Name))
						return false;
				}
				hashEntries.AddRange(filteredHashEnttries);
				if(isArray)
				{
					var arrayType = prop.PropertyType.GetElementType();
					var listType = typeof(List<>);
					var constructedListType = listType.MakeGenericType(arrayType);
					var listInstance = Activator.CreateInstance(constructedListType);
					foreach(var hashEntry in hashEntries)
					{
						var valueInstance = Activator.CreateInstance(arrayType);
						var value = JsonConvert.DeserializeAnonymousType(hashEntry.Value, valueInstance);
						if(value is string)
							listInstance.GetType().GetMethod("Add").Invoke(listInstance, new[] {value});
						var jObj = (JObject)value;
						var validValue = jObj.ToObject(arrayType);
						listInstance.GetType().GetMethod("Add").Invoke(listInstance, new[] {validValue});
					}
					var arrayPropValue = listInstance.GetType().GetRuntimeMethod("ToArray", new Type[] {}).Invoke(listInstance, new object[] {});
					prop.SetValue(entity, arrayPropValue);
				}
				var collectionType = prop.PropertyType.GetGenericTypeDefinition();
				var typeArgs = GetCollectionType(prop.PropertyType);
				var constructedType = collectionType.MakeGenericType(typeArgs);
				var constructedInstance = Activator.CreateInstance(constructedType);
				foreach(var hashEntry in hashEntries)
				{
					var value = JsonConvert.DeserializeObject(hashEntry.Value.ToString());
					if(typeArgs.Length < 2)
					{
						if(value is string)
							constructedInstance.GetType().GetMethod("Add").Invoke(constructedInstance, new[] {value});
						var jObj = (JObject)value;
						var validValue = jObj.ToObject(typeArgs[0]);
						constructedInstance.GetType().GetMethod("Add").Invoke(constructedInstance, new[] {validValue});
					}
					var parsedKey = hashEntry.Name.ToString().Substring(prop.Name.Length + 1);
					var hashKey = JsonConvert.DeserializeObject(parsedKey, typeArgs[0]);
					object validKey;
					object validVal;
					if(hashKey is string)
						validKey = hashKey;
					var keyObj = (JObject)hashKey;
					validKey = keyObj.ToObject(typeArgs[0]);
					if(value is string)
						validVal = value;
					var valueJObj = (JObject)value;
					validVal = valueJObj.ToObject(typeArgs[1]);
					constructedInstance.GetType().GetMethod("Add").Invoke(constructedInstance, new[] {validKey, validVal});
				}
				prop.SetValue(entity, constructedInstance);
				return true;
			}
			return false;
		}

		private static object GetConstructedDictionaryPropertyInstance(Type fieldType, List<HashEntry> dictionaryHashes, string propertyName)
		{
			var collectionType = fieldType.GetGenericTypeDefinition();
			var typeArgs = GetCollectionType(fieldType);
			var constructedType = collectionType.MakeGenericType(typeArgs);
			var constructedInstance = Activator.CreateInstance(constructedType);
			foreach (var hashEntry in dictionaryHashes)
			{
				var value = JsonConvert.DeserializeObject(hashEntry.Value.ToString());
				var parsedKey = hashEntry.Name.ToString().Substring(propertyName.Length + 1);
				var hashKey = JsonConvert.DeserializeObject(parsedKey, typeArgs[0]);
				object validKey;
				object validVal;
				if (hashKey is string)
					validKey = hashKey;
				else if (hashKey is int)
					validKey = hashKey;
				else
				{
					var keyObj = (JObject)hashKey;
					validKey = keyObj.ToObject(typeArgs[0]);
				}
				if (value is string)
					validVal = value;
				else
				{
					var valueJObj = (JObject)value;
					validVal = valueJObj.ToObject(typeArgs[1]);
				}
				constructedInstance.GetType().GetMethod("Add").Invoke(constructedInstance, new[] { validKey, validVal });
			}
			return constructedInstance;
		}

		private static Type[] GetCollectionType(Type type)
		{
			foreach(var typeInterface in type.GetInterfaces())
			{
				if(typeInterface.IsGenericType && typeInterface.GetGenericTypeDefinition() == typeof(IDictionary<,>))
					return typeInterface.GetGenericArguments();
				if(typeInterface.IsGenericType && typeInterface.GetGenericTypeDefinition() == typeof(ICollection<>))
					return typeInterface.GetGenericArguments();
			}
			return null;
		}
	}
}
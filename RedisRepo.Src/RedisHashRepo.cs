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

		public RedisHash(RedisConfig redisConfig)
		{
			RedisDatabase = redisConfig.RedisMultiplexer.GetDatabase(redisConfig.RedisDatabaseId);
			FlattenDictionaries = true;
		}

		public bool FlattenDictionaries { get; set; }

		public Func<T, string> PrimaryEntityId
		{
			get { return _primaryEntityId ?? (_primaryEntityId = GetDefaultEntityId); }
			set { _primaryEntityId = value; }
		}

		public IDatabase RedisDatabase { get; private set; }

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
			await RedisDatabase.HashSetAsync(ComposePrimaryCacheKey(PrimaryEntityId(entity)), hashEntries.ToArray()).ConfigureAwait(false);
		}

		public async Task<T> GetAllAsync(string entityId)
		{
			var redisKey = ComposePrimaryCacheKey(entityId);
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
			var entityId = PrimaryEntityId(entity);
			var redisKey = ComposePrimaryCacheKey(entityId);
			await RedisDatabase.KeyDeleteAsync(redisKey).ConfigureAwait(false);
		}

		public async Task<TFieldValue> GetFieldVaueAsync<TFieldValue>(T entity, Expression<Func<T, object>> propertyExpression)
		{
			var entityId = PrimaryEntityId(entity);
			var redisKey = ComposePrimaryCacheKey(entityId);
			var hashName = GetPropertyName(propertyExpression);
			return await GetFieldVaueAsync<TFieldValue>(redisKey, hashName).ConfigureAwait(false);
		}

		public async Task<TFieldValue> GetDictionaryFieldValueAsync<TFieldValue>(T entity, Expression<Func<T, object>> propertyExpression,
		                                                                               object dictionaryKey)
		{
			var entityId = PrimaryEntityId(entity);
			var redisKey = ComposePrimaryCacheKey(entityId);
			var hashPropertyName = GetPropertyName(propertyExpression);
			var key = JsonConvert.SerializeObject(dictionaryKey);
			var hashName = ComposeCollectionHashFieldName(hashPropertyName, key);
			return await GetFieldVaueAsync<TFieldValue>(redisKey, hashName).ConfigureAwait(false);
		}

		public async Task<TFieldValue> GetFieldVaueAsync<TFieldValue>(string redisKey, string hashName)
		{
			var hashVal = await RedisDatabase.HashGetAsync(redisKey, hashName).ConfigureAwait(false);
			var val = JsonConvert.DeserializeObject<TFieldValue>(hashVal);
			return val;
		}

		public async Task SetFieldValueAsync<TFieldValue>(T entity, Expression<Func<T, object>> propertyExpression, TFieldValue value)
		{
			var entityId = PrimaryEntityId(entity);
			var redisKey = ComposePrimaryCacheKey(entityId);
			var hashName = GetPropertyName(propertyExpression);
			var serializedObj = JsonConvert.SerializeObject(value);
			await RedisDatabase.HashSetAsync(redisKey, hashName, serializedObj).ConfigureAwait(false);
		}

		public async Task SetDictionaryFieldValueAsync<TFieldValue>(T entity, Expression<Func<T, object>> propertyExpression, object dictionaryKey,
		                                                            TFieldValue value)
		{
			var entityId = PrimaryEntityId(entity);
			var redisKey = ComposePrimaryCacheKey(entityId);
			var propertyName = GetPropertyName(propertyExpression);
			var serializedKey = JsonConvert.SerializeObject(dictionaryKey);
			var hashName = ComposeCollectionHashFieldName(propertyName, serializedKey);
			var serializedObj = JsonConvert.SerializeObject(value);
			await RedisDatabase.HashSetAsync(redisKey, hashName, serializedObj).ConfigureAwait(false);
		}

		public async Task<bool> ExistsAsync(T entity)
		{
			var entityId = PrimaryEntityId(entity);
			var redisKey = ComposePrimaryCacheKey(entityId);
			return await RedisDatabase.KeyExistsAsync(redisKey).ConfigureAwait(false);
		}

		public async Task<bool> ExistsAsync(string redisKey) { return await RedisDatabase.KeyExistsAsync(redisKey).ConfigureAwait(false); }

		public string ComposePrimaryCacheKey(string entityId) { return string.Format("ObjectHash:{0}:{1}", typeof(T).Name, entityId); }

		public string ComposeCollectionHashFieldName(string propertyName, string collectionKey)
		{
			var name = string.Format("{0}:{1}", propertyName, collectionKey);
			return name;
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

		private static string GetPropertyName(Expression<Func<T, object>> propertyExpression)
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
					var itemFieldHashName = ComposeCollectionHashFieldName(hashFieldName, keySerialized);
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
				// will be fairly complex when doing it generically.
				return false;
				var index = 1;
				foreach(var listItem in (ICollection)propertyInfo.GetValue(entity))
				{
					var itemFieldHashName = ComposeCollectionHashFieldName(hashFieldName, index.ToString());
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
				var collectionType = prop.PropertyType.GetGenericTypeDefinition();
				var typeArgs = GetCollectionType(prop.PropertyType);
				var constructedType = collectionType.MakeGenericType(typeArgs);
				var constructedInstance = Activator.CreateInstance(constructedType);
				foreach(var hashEntry in hashEntries)
				{
					var value = JsonConvert.DeserializeObject(hashEntry.Value.ToString());
					var parsedKey = hashEntry.Name.ToString().Substring(prop.Name.Length + 1);
					var hashKey = JsonConvert.DeserializeObject(parsedKey, typeArgs[0]);
					object validKey;
					object validVal;
					if(hashKey is string)
						validKey = hashKey;
					else if(hashKey is int)
						validKey = hashKey;
					else
					{
						var keyObj = (JObject)hashKey;
						validKey = keyObj.ToObject(typeArgs[0]);
					}
					if(value is string)
						validVal = value;
					else
					{
						var valueJObj = (JObject)value;
						validVal = valueJObj.ToObject(typeArgs[1]);
					}
					constructedInstance.GetType().GetMethod("Add").Invoke(constructedInstance, new[] {validKey, validVal});
				}
				prop.SetValue(entity, constructedInstance);
				return true;
			}
			var isICollection = propInterfaces.Any(pi => pi.IsGenericType && pi.GetGenericTypeDefinition() == typeof(ICollection<>));
			var isArray = typeof(Array).IsAssignableFrom(prop.PropertyType);

			//if(prop.PropertyType.Name == "List`1")
			if(isICollection || isArray)
			{
				// We aren't going to flatten lists right now because getting and setting the individual items in a collection
				// will be fairly complex when doing it generically.
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
						else
						{
							var jObj = (JObject)value;
							var validValue = jObj.ToObject(arrayType);
							listInstance.GetType().GetMethod("Add").Invoke(listInstance, new[] {validValue});
						}
					}
					var arrayPropValue = listInstance.GetType().GetRuntimeMethod("ToArray", new Type[] {}).Invoke(listInstance, new object[] {});
					prop.SetValue(entity, arrayPropValue);
				}
				else
				{
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
							else
							{
								var jObj = (JObject)value;
								var validValue = jObj.ToObject(typeArgs[0]);
								constructedInstance.GetType().GetMethod("Add").Invoke(constructedInstance, new[] {validValue});
							}
						}
						else
						{
							var parsedKey = hashEntry.Name.ToString().Substring(prop.Name.Length + 1);
							var hashKey = JsonConvert.DeserializeObject(parsedKey, typeArgs[0]);
							object validKey;
							object validVal;
							if(hashKey is string)
								validKey = hashKey;
							else
							{
								var keyObj = (JObject)hashKey;
								validKey = keyObj.ToObject(typeArgs[0]);
							}
							if(value is string)
								validVal = value;
							else
							{
								var valueJObj = (JObject)value;
								validVal = valueJObj.ToObject(typeArgs[1]);
							}
							constructedInstance.GetType().GetMethod("Add").Invoke(constructedInstance, new[] {validKey, validVal});
						}
					}
					prop.SetValue(entity, constructedInstance);
				}
				return true;
			}
			return false;
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
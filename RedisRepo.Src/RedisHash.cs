using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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

		public RedisHash(RedisConfig redisConfig) { RedisDatabase = redisConfig.RedisMultiplexer.GetDatabase(redisConfig.RedisDatabaseId); }

		public bool FlattenCollections { get; set; }

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
				if(CanGetHashItemsFromCollection(propertyInfo, propInterfaces, hashFieldName, hashEntries, entity))
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

		public async Task DeleteAsync(T entity) {  }

		public async Task<TFieldValue> GetFieldVaueAsync<TFieldValue>(Expression<Func<T, object>> propertyExpression) { return default(TFieldValue); }

		public async Task<TFieldValue> GetFieldVaueAsync<TFieldValue>(string redisKey, string hashName) { return default(TFieldValue); }

		public async Task SetFieldValueAsync(Expression<Func<T, object>> propertyExpression) {  }

		public async Task<bool> ExistsAsync(T entity) { return false; }

		public async Task<bool> ExistsAsync(string redisKey) { return false; }

		public string ComposePrimaryCacheKey(string entityId) { return string.Format("ObjectHash:{0}:{1}", typeof(T).Name, entityId); }

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

		public string ComposeCollectionHashFieldName(string propertyName, string collectionKey)
		{
			var name = string.Format("{0}:{1}", propertyName, collectionKey);
			return name;
		}

		private string GetPropertyName(Expression<Func<T, object>> propertyExpression)
		{
			var body = propertyExpression.Body;
			var convertExpression = body as UnaryExpression;
			if(convertExpression == null)
				return ((MemberExpression)body).Member.Name;
			if(convertExpression.NodeType != ExpressionType.Convert)
				throw new ArgumentException("Invalid property expression.", "exp");
			body = convertExpression.Operand;
			return ((MemberExpression)body).Member.Name;
		}

		private static bool CanPutHashItemsIntoCollection(PropertyInfo prop, Type[] propInterfaces, List<HashEntry> hash, T entity)
		{
			var hashEntries = new List<HashEntry>();
			//if (prop.PropertyType.Name == "Dictionary`2")
			if(false)
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
				var dictionaryProperty = new Dictionary<object, object>();
				foreach(var hashEntry in hashEntries)
				{
					var parsedKey = hashEntry.Name.ToString().Substring(prop.Name.Length + 1);
					var key = JsonConvert.DeserializeObject(parsedKey);
					var value = JsonConvert.DeserializeObject(hashEntry.Value.ToString());
					if(!dictionaryProperty.ContainsKey(key))
						dictionaryProperty.Add(key, value);
				}
				prop.SetValue(entity, dictionaryProperty);
				return true;
			}

			var isICollection = propInterfaces.Any(pi => pi.IsGenericType && pi.GetGenericTypeDefinition() == typeof(ICollection<>));
			//if(prop.PropertyType.Name == "List`1")
			//if(typeof(ICollection<>).IsAssignableFrom(prop.PropertyType))
			if(isICollection)
			{
				var filteredHashEnttries = hash.Where(h => h.Name.ToString().Contains(prop.Name)).ToList();
				if (filteredHashEnttries.Count < 1)
					return true;

				// See if there is just a single hash entry
				if (filteredHashEnttries.Count == 1)
				{
					var entry = filteredHashEnttries.FirstOrDefault();
					if (string.Equals(entry.Name.ToString(), prop.Name))
						return false;
				}
				hashEntries.AddRange(filteredHashEnttries);
				var collectionType = prop.PropertyType.GetGenericTypeDefinition();
				var typeArgs = GetCollectionType(prop.PropertyType);
				var constructedType = collectionType.MakeGenericType(typeArgs);
				var constructedInstance = Activator.CreateInstance(constructedType);
				foreach (var hashEntry in hashEntries)
				{
					var value = JsonConvert.DeserializeObject(hashEntry.Value.ToString());
					if(typeArgs.Length < 2)
						constructedInstance.GetType().GetMethod("Add").Invoke(constructedInstance, new[] {value});
					else
					{
						var parsedKey = hashEntry.Name.ToString().Substring(prop.Name.Length + 1);
						var hashKey = JsonConvert.DeserializeObject(parsedKey, typeArgs[0]);
						constructedInstance.GetType().GetMethod("Add").Invoke(constructedInstance, new[] {hashKey, value});
					}
					
				}
				
				prop.SetValue(entity, constructedInstance);
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

		private bool CanGetHashItemsFromCollection(PropertyInfo propertyInfo, Type[] propInterfaces, string hashFieldName, List<HashEntry> hashEntries,
												T entity)
		{
			HashEntry hashEntry;
			//if (propInterfaces.Contains(typeof(IDictionary)))
			if(propertyInfo.PropertyType.Name == "Dictionary`2")
			{
				foreach (var item in (IDictionary)propertyInfo.GetValue(entity))
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
			
			if (propInterfaces.Contains(typeof(ICollection)))
			{
				var index = 1;
				foreach (var listItem in (ICollection)propertyInfo.GetValue(entity))
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
	}
}
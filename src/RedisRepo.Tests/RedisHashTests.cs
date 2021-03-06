﻿using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Newtonsoft.Json;

using Nito.AsyncEx.Synchronous;

using RedisRepo.Src;
using RedisRepo.Tests.MockData;
using RedisRepo.Tests.ServiceLocator;

namespace RedisRepo.Tests
{
	[TestClass]
	public class RedisHashTests
	{
		private readonly List<UserInfo> _users = new List<UserInfo>();
		private readonly IAppCache _appCache;
		private readonly IRedisHash<UserInfo> _userRedisHash;

		public RedisHashTests()
		{
			Startup.Run();
			_appCache = AppServiceLocator.Current.GetInstance<IAppCache>();
			_userRedisHash = AppServiceLocator.Current.GetInstance<IRedisHash<UserInfo>>();
			_userRedisHash.FlattenDictionaries = true;

			// This isn't truly necessary since the default value of PromaryEntityId is a method that uses reflection to 
			// look for a property with a name of Id, UserInfoId, or EntityId;
			_userRedisHash.PrimaryEntityIdLocator = info => info.Id.ToString();
		}

		[TestInitialize]
		public void Setup()
		{
			for(var i = 1; i < 12; i++)
			{
				var user = new UserInfo
				{
					Id = i,
					FirstName = "FirstName_" + i,
					LastName = "LastName_" + i,
					Username = "Username_" + i
				};
				for(var innerI = 1; innerI < 6; innerI++)
				{
					var email = string.Format("{0}.{1}.{2}@SomeEmail.com", user.FirstName, user.LastName, innerI);
					user.Emails.Add(email);
				}
				for(var j = 1; j < 4; j++)
				{
					user.SomeCollection.Add(j, new AppRole {Id = j, Name = "Dictionary Role " + j, RoleGroup = "DictionaryRoles"});
				}
				var rolesList = new List<AppRole>();
				for(var j = 1; j < 4; j++)
				{
					rolesList.Add(new AppRole
					{
						Id = j,
						Name = "App Role " + j,
						RoleGroup = "User Roles"
					});
				}
				user.Roles = rolesList.ToArray();
				_users.Add(user);
			}
		}

		[TestCleanup]
		public void TearDown() { _appCache.ClearCache(); }

		[TestMethod]
		public void ShouldAddRedisHashFromObjectInstance()
		{
			// Arrange
			var givenUser = _users[0];
			var primaryCacheKey = _userRedisHash.PrimaryCacheKeyFormatter(givenUser.Id.ToString());

			// Act
			_userRedisHash.SetAllAsync(givenUser).WaitAndUnwrapException();
			var firstName =
				JsonConvert.DeserializeObject<string>(_userRedisHash.RedisDatabase.HashGet(primaryCacheKey, "FirstName"));
			var lastName =
				JsonConvert.DeserializeObject<string>(_userRedisHash.RedisDatabase.HashGet(primaryCacheKey, "LastName"));

			// Assert
			Assert.IsTrue(string.Equals(firstName, givenUser.FirstName));
			Assert.IsTrue(string.Equals(lastName, givenUser.LastName));
		}

		[TestMethod]
		public void ShouldGetObjectFromRedisHash()
		{
			// Arrange
			var givenUser = _users[0];
			var primaryCacheKey = _userRedisHash.PrimaryCacheKeyFormatter(givenUser.Id.ToString());
			_userRedisHash.SetAllAsync(givenUser).WaitAndUnwrapException();

			// Act
			var gottenUser = _userRedisHash.GetAllAsync(givenUser.Id.ToString()).WaitAndUnwrapException();

			// Assert
			Assert.IsNotNull(gottenUser);
			Assert.IsTrue(string.Equals(givenUser.FirstName, gottenUser.FirstName));
			Assert.IsTrue(string.Equals(givenUser.LastName, gottenUser.LastName));
			Assert.IsTrue(givenUser.Emails.Count == gottenUser.Emails.Count);
			Assert.IsTrue(gottenUser.SomeCollection.ContainsKey(2));
		}

		[TestMethod]
		public void ShouldSetSingleHashField()
		{
			// Arrange
			var givenUser = _users[0];
			var primaryCacheKey = _userRedisHash.PrimaryCacheKeyFormatter(givenUser.Id.ToString());
			_userRedisHash.SetAllAsync(givenUser).WaitAndUnwrapException();

			// Act
			givenUser.FirstName = "Brian";
			_userRedisHash.SetFieldValueAsync(givenUser.Id.ToString(), info => info.FirstName, givenUser.FirstName).WaitAndUnwrapException();
			var gottenUser = _userRedisHash.GetAllAsync(givenUser.Id.ToString()).WaitAndUnwrapException();

			// Assert
			Assert.IsNotNull(gottenUser);
			Assert.IsTrue(string.Equals(givenUser.FirstName, gottenUser.FirstName));
		}

		[TestMethod]
		public void ShouldGetSingleHashField()
		{
			// Arrange
			var givenUser = _users[0];
			var primaryCacheKey = _userRedisHash.PrimaryCacheKeyFormatter(givenUser.Id.ToString());
			_userRedisHash.SetAllAsync(givenUser).WaitAndUnwrapException();

			// Act
			givenUser.FirstName = "Brian";
			_userRedisHash.SetFieldValueAsync(givenUser.Id.ToString(), info => info.FirstName, givenUser.FirstName).WaitAndUnwrapException();
			var firstName = _userRedisHash.GetFieldVaueAsync(givenUser.Id.ToString(), info => info.FirstName).WaitAndUnwrapException();

			// Assert
			Assert.IsTrue(string.Equals(givenUser.FirstName, firstName));
		}

		[TestMethod]
		public void ShouldSetSingleDictionaryHashField()
		{
			// Arrange
			var givenUser = _users[0];
			var primaryCacheKey = _userRedisHash.PrimaryCacheKeyFormatter(givenUser.Id.ToString());
			_userRedisHash.SetAllAsync(givenUser).WaitAndUnwrapException();

			// Act
			givenUser.SomeCollection.Remove(2);
			var newRole = new AppRole {Id = 44, Name = "My Custom Role", RoleGroup = "Changed Role Group"};
			givenUser.SomeCollection.Add(2, newRole);
			_userRedisHash.SetDictionaryFieldValueAsync(givenUser.Id.ToString(), info => info.SomeCollection, 2, newRole).WaitAndUnwrapException();
			var gottenUser = _userRedisHash.GetAllAsync(givenUser.Id.ToString()).WaitAndUnwrapException();
			AppRole gottenNewRole;
			AppRole givenNewRole;
			gottenUser.SomeCollection.TryGetValue(2, out gottenNewRole);
			givenUser.SomeCollection.TryGetValue(2, out givenNewRole);

			// Assert
			Assert.IsNotNull(gottenUser);
			Assert.IsTrue(string.Equals(gottenNewRole.Name, givenNewRole.Name));
		}

		[TestMethod]
		public void ShouldGetSingleDictionaryHashField()
		{
			// Arrange
			var givenUser = _users[0];
			var primaryCacheKey = _userRedisHash.PrimaryCacheKeyFormatter(givenUser.Id.ToString());
			_userRedisHash.SetAllAsync(givenUser).WaitAndUnwrapException();

			// Act
			givenUser.SomeCollection.Remove(2);
			var newRole = new AppRole {Id = 44, Name = "My Custom Role", RoleGroup = "Changed Role Group"};
			givenUser.SomeCollection.Add(2, newRole);
			_userRedisHash.SetDictionaryFieldValueAsync(givenUser.Id.ToString(), info => info.SomeCollection, 2, newRole).WaitAndUnwrapException();
			var gottenUser = _userRedisHash.GetAllAsync(givenUser.Id.ToString()).WaitAndUnwrapException();
			var gottenNewRole = _userRedisHash.GetDictionaryFieldValueAsync<AppRole>(givenUser.Id.ToString(), info => info.SomeCollection, 2).WaitAndUnwrapException();
			AppRole givenNewRole;

			//gottenUser.SomeCollection.TryGetValue(2, out gottenNewRole);
			givenUser.SomeCollection.TryGetValue(2, out givenNewRole);

			// Assert
			Assert.IsNotNull(gottenUser);
			Assert.IsTrue(string.Equals(gottenNewRole.Name, givenNewRole.Name));
		}

		[TestMethod]
		public void ShouldGetFullDictionaryObject()
		{
			// Arrange
			var givenUser = _users[0];
			var primaryCacheKey = _userRedisHash.PrimaryCacheKeyFormatter(givenUser.Id.ToString());
			_userRedisHash.SetAllAsync(givenUser).WaitAndUnwrapException();

			// Act
			givenUser.SomeCollection.Remove(2);
			var newRole = new AppRole { Id = 44, Name = "My Custom Role", RoleGroup = "Changed Role Group" };
			givenUser.SomeCollection.Add(2, newRole);
			_userRedisHash.SetDictionaryFieldValueAsync(givenUser.Id.ToString(), info => info.SomeCollection, 2, newRole).WaitAndUnwrapException();
			var gottenUser = _userRedisHash.GetAllAsync(givenUser.Id.ToString()).WaitAndUnwrapException();
			var gottenDictionary = _userRedisHash.GetFieldVaueAsync(givenUser.Id.ToString(), info => info.SomeCollection).WaitAndUnwrapException();
			var gottenNewRole = _userRedisHash.GetDictionaryFieldValueAsync<AppRole>(givenUser.Id.ToString(), info => info.SomeCollection, 2).WaitAndUnwrapException();
			AppRole givenNewRole;

			//gottenUser.SomeCollection.TryGetValue(2, out gottenNewRole);
			givenUser.SomeCollection.TryGetValue(2, out givenNewRole);

			// Assert
			Assert.IsNotNull(gottenUser);
			Assert.IsTrue(gottenDictionary[1].Name == givenUser.SomeCollection[1].Name);
			Assert.IsTrue(string.Equals(gottenNewRole.Name, givenNewRole.Name));
		}
	}
}
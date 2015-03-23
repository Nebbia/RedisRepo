using System;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Newtonsoft.Json;

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

			// This isn't truly necessary since the default value of PromaryEntityId is a method that uses reflection to 
			// look for a property with a name of Id, UserInfoId, or EntityId;
			_userRedisHash.PrimaryEntityId = info => info.Id.ToString();
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
					user.SomeCollection.Add(user.Username + "-" + j, new AppRole {Id = j, Name = "Dictionary Role " + j, RoleGroup = "DictionaryRoles"});
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
			var primaryCacheKey = _userRedisHash.ComposePrimaryCacheKey(givenUser.Id.ToString());

			// Act
			_userRedisHash.SetAllAsync(givenUser).Wait();
			var firstName =
				JsonConvert.DeserializeObject<string>(_userRedisHash.RedisDatabase.HashGet(primaryCacheKey, "FirstName"));
			var lastName =
				JsonConvert.DeserializeObject<string>(_userRedisHash.RedisDatabase.HashGet(primaryCacheKey, "LastName"));
			var email1 =
				JsonConvert.DeserializeObject<string>(
					_userRedisHash.RedisDatabase.HashGet(primaryCacheKey, _userRedisHash.ComposeCollectionHashFieldName("Emails", "1")));
			var appRole1 =
				JsonConvert.DeserializeObject<AppRole>(
					_userRedisHash.RedisDatabase.HashGet(primaryCacheKey, _userRedisHash.ComposeCollectionHashFieldName("Roles", "1")));

			// Assert
			Assert.IsTrue(string.Equals(firstName, givenUser.FirstName));
			Assert.IsTrue(string.Equals(lastName, givenUser.LastName));
			Assert.IsTrue(string.Equals(email1, string.Format("{0}.{1}.{2}@SomeEmail.com", givenUser.FirstName, givenUser.LastName, 1)));
			Assert.IsTrue(string.Equals(givenUser.Roles[0].Name, appRole1.Name));
		}

		[TestMethod]
		public void ShouldGetObjectFromRedisHash()
		{
			// Arrange
			var givenUser = _users[0];
			var primaryCacheKey = _userRedisHash.ComposePrimaryCacheKey(givenUser.Id.ToString());
			_userRedisHash.SetAllAsync(givenUser).Wait();

			// Act
			var gottenUser = _userRedisHash.GetAllAsync(givenUser.Id.ToString()).Result;

			// Assert
			Assert.IsNotNull(gottenUser);
			Assert.IsTrue(string.Equals(givenUser.FirstName, gottenUser.FirstName));
		}
	}
}
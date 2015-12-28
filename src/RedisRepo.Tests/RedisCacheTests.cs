using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Nito.AsyncEx.Synchronous;

using RedisRepo.Src;
using RedisRepo.Tests.MockData;
using RedisRepo.Tests.ServiceLocator;

namespace RedisRepo.Tests
{
	[TestClass]
	public class RedisCacheTests
	{
		private readonly List<UserInfo> _users = new List<UserInfo>();
		private readonly IAppCache _appCache;
		private readonly List<AppRole> _rolesList = new List<AppRole>();
 
		public RedisCacheTests ()
		{
			Startup.Run();
			_appCache = AppServiceLocator.Current.GetInstance<IAppCache>();
		}

		[TestInitialize]
		public void Setup()
		{
			for (var j = 1; j < 4; j++)
			{
				_rolesList.Add(new AppRole
				{
					Id = j,
					Name = "App Role " + j,
					RoleGroup = "User Roles"
				});
			}
			for (var i = 1; i < 24; i++)
			{
				var user = new UserInfo
				{
					Id = i,
					FirstName = "FirstName_" + i,
					LastName = "LastName_" + i,
					Username = "Username_" + i
				};
				for (var innerI = 1; innerI < 6; innerI++)
				{
					var email = $"{user.FirstName}.{user.LastName}.{innerI}@SomeEmail.com";
					user.Emails.Add(email);
				}
				for (var j = 1; j < 4; j++)
				{
					user.SomeCollection.Add(j, new AppRole { Id = j, Name = "Dictionary Role " + j, RoleGroup = "DictionaryRoles" });
				}
				user.Roles = i % 2 == 0 ? _rolesList.Skip(0).Take(3).ToArray() : _rolesList.Skip(3).Take(1).ToArray();
				_users.Add(user);
			}
		}

		[TestCleanup]
		public void TearDown() { _appCache.ClearCache(); }

		[TestMethod]
		public void ShouldSaveItemsIntoPartition()
		{
			// Arrange
			foreach(var userInfo in _users)
			{
				_appCache.AddOrUpdateAsync(userInfo.EntityId.ToString(), userInfo, null, "Users").WaitAndUnwrapException();
			}

			// Act
			var user1 = _appCache.GetValueAsync<UserInfo>(_users[0].EntityId.ToString(), "Users").WaitAndUnwrapException();
			var allUsers = _appCache.GetAllItemsInPartitionAsync<UserInfo>("Users").WaitAndUnwrapException();

			// Assert
			Assert.IsNotNull(user1);
			Assert.AreEqual(user1.EntityId, _users[0].EntityId);
			Assert.AreEqual(allUsers.Count, _users.Count);
		}

		[TestMethod]
		public void ShouldAddEmailsToCacheIndex()
		{
			// Arrange
			string roleToSearchFor = _rolesList[2].Name;
			var expectedRolesList = new List<AppRole>();
			foreach(var userInfo in _users)
			{
				_appCache.AddOrUpdateAsync(userInfo.EntityId.ToString(), userInfo, null, "Users").WaitAndUnwrapException();
				foreach(var email in userInfo.Emails)
				{
					_appCache.AddOrUpdateItemOnCustomIndexAsync("UserEmails", email, userInfo.EntityId.ToString(), "Users")
					         .WaitAndUnwrapException();
				}
				foreach(var appRole in userInfo.Roles)
				{
					if(appRole.Name == roleToSearchFor)
						expectedRolesList.Add(appRole);
					_appCache.AddOrUpdateItemOnCustomIndexAsync("UsersForRole", appRole.Name, userInfo.EntityId.ToString(), "Users")
					         .WaitAndUnwrapException();
				}
			}
			
			// Act
			var users0 = _users[0];
			var users0Email0 = users0.Emails[0];
			var user1 = (_appCache.FindAsync<UserInfo>("UserEmails", _users[0].Emails[0], "Users").WaitAndUnwrapException()).FirstOrDefault();
			var user1Email1 = user1.Emails[0];
			var usersForRole = _appCache.FindAsync<UserInfo>("UsersForRole", roleToSearchFor, "Users").WaitAndUnwrapException();

			// Assert
			Assert.AreEqual(user1.EntityId, users0.EntityId);
			Assert.AreEqual(users0Email0, user1Email1);
			Assert.IsTrue(usersForRole.Count == expectedRolesList.Count);
		}
	}
}
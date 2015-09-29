using System.Collections.Generic;

namespace RedisRepo.Tests.MockData
{
	public class UserInfo : BaseEntity
	{
		private List<string> _emails;
		private Dictionary<int, AppRole> _someCollection;
		private AppRole[] _roles;

		public string Username { get; set; }

		public string FirstName { get; set; }

		public string LastName { get; set; }

		public List<string> Emails { get { return _emails ?? (_emails = new List<string>()); } set { _emails = value; } }

		public Dictionary<int, AppRole> SomeCollection
		{
			get { return _someCollection ?? (_someCollection = new Dictionary<int, AppRole>()); }
			set { _someCollection = value; }
		}

		public AppRole[] Roles { get { return _roles ?? (_roles = new AppRole[1]); } set { _roles = value; } }
	}
}
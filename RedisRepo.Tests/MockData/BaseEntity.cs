using System;

namespace RedisRepo.Tests.MockData
{
	public class BaseEntity
	{
		private Guid _entityId;

		public int Id { get; set; }

		public Guid EntityId { get { return _entityId == default(Guid) ? (_entityId = Guid.NewGuid()) : _entityId; } set { _entityId = value; } }
	}
}
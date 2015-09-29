using System;

namespace RedisRepo.Tests.ServiceLocator
{
	public class AppServiceLocator
	{
		public static IAppServiceLocator Current { get; private set; }

		public static void SetServiceLocator(Func<IAppServiceLocator> create)
		{
			if (create == null)
				throw new ArgumentNullException("create");
			Current = create();
		}
	}
}
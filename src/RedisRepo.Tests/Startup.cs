using Ninject;

using RedisRepo.Tests.ServiceLocator;

namespace RedisRepo.Tests
{
	public class Startup
	{
		public static void Run()
		{
			var kernel = new StandardKernel(new NinjectRegistrar());
			AppServiceLocator.SetServiceLocator(() => new NinjectServiceLocator(kernel));
		}
	}
}
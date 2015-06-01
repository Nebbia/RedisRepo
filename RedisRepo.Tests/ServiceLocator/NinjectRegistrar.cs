using Ninject;
using Ninject.Activation;
using Ninject.Modules;
using Ninject.Extensions.Conventions;

using RedisRepo.Src;

namespace RedisRepo.Tests.ServiceLocator
{
	public class NinjectRegistrar : NinjectModule
	{
		public override void Load()
		{
			Kernel.Bind(x => x
				.FromAssembliesMatching("*")
				.SelectAllClasses()
				.BindDefaultInterface());
			Bind<IAppCache>().To<RedisCache>();
			Bind<RedisConfig>().ToProvider<RedisConfigProvider>().InSingletonScope();
			Bind(typeof(ICacheRepo<>)).To(typeof(CacheRepo<>));
			Bind(typeof(IRedisHash<>)).To(typeof(RedisHash<>));
		}
	}

	public class RedisConfigProvider : Provider<RedisConfig>
	{
		protected override RedisConfig CreateInstance(IContext context)
		{
			var redisConfig = new RedisConfig("localhost,allowAdmin=true", 7);
			return redisConfig;
		}
	}
}
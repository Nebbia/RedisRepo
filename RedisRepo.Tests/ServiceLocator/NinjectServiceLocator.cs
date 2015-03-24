using System;

using Ninject;

namespace RedisRepo.Tests.ServiceLocator
{
	public class NinjectServiceLocator : IAppServiceLocator
	{
		private readonly IKernel _kernel;
		public NinjectServiceLocator (IKernel kernel) {
			_kernel = kernel;
		}

		public T GetInstance<T>() where T : class { return _kernel.Get<T>(); }

		public T GetNamedInstance<T>(string instanceName) { return _kernel.Get<T>(instanceName); }

		public object GetInstance(Type type) { return _kernel.Get(type); }
	}
}
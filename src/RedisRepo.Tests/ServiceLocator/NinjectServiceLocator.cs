using System;
using System.Linq;

using Ninject;
using Ninject.Parameters;

namespace RedisRepo.Tests.ServiceLocator
{
	public class NinjectServiceLocator : IAppServiceLocator
	{
		private readonly IKernel _kernel;
		public NinjectServiceLocator (IKernel kernel) {
			_kernel = kernel;
		}

		public T GetInstance<T>(params object[] argumentParameters) where T : class
		{
			return argumentParameters.Length > 0
				? _kernel.Get<T>(argumentParameters.OfType<ConstructorArgument>().Cast<IParameter>().ToArray()) : _kernel.Get<T>();
		}

		public T GetNamedInstance<T>(string instanceName) { return _kernel.Get<T>(instanceName); }

		public object GetInstance(Type type) { return _kernel.Get(type); }
	}
}
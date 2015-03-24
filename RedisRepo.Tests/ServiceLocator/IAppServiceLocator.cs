using System;

namespace RedisRepo.Tests.ServiceLocator
{
	public interface IAppServiceLocator
	{
		/// <summary>
		/// Returns an instance based on the generic type provided.
		/// </summary>
		/// <typeparam name="T">Type of instance needed</typeparam>
		/// <returns></returns>
		T GetInstance<T>() where T : class;

		/// <summary>
		/// Gets a pre-configured instance of <typeparam name="T"></typeparam> by its instance name from the container.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="instanceName"></param>
		/// <returns></returns>
		T GetNamedInstance<T>(string instanceName);

		/// <summary>
		/// Returns an instance from the IoC container based on the type provided.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		object GetInstance(Type type);
	}
}
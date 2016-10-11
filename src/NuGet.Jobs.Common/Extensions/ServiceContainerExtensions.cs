using System.ComponentModel.Design;

namespace NuGet.Jobs
{
    public static class ServiceContainerExtensions
    {
        public static T GetService<T>(this IServiceContainer container)
        {
            return (T)container.GetService(typeof(T));
        }

        public static void AddService<T>(this IServiceContainer container, T serviceInstance)
        {
            container.AddService(typeof(T), serviceInstance);
        }
    }
}

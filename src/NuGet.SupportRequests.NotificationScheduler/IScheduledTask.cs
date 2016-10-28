using System.Threading.Tasks;

namespace NuGet.SupportRequests.NotificationScheduler
{
    internal interface IScheduledTask
    {
        Task Run();
    }
}

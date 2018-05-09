using System.Threading.Tasks;
using NuGet.Jobs.Validation.ScanAndSign;

namespace NuGet.Services.Validation.Orchestrator.PackageSigning.ScanAndSign
{
    public interface IScanAndSignEnqueuer
    {
        Task EnqueueVerificationAsync(IValidationRequest request, OperationRequestType requestType);
    }
}
using System.Threading;
using System.Threading.Tasks;

namespace Mosaic.Bridge.Core.Server
{
    public interface IRequestHandler
    {
        Task<HandlerResponse> HandleAsync(HandlerRequest request, CancellationToken ct);
    }
}

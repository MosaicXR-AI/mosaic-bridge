using Mosaic.Bridge.Core.Server;

namespace Mosaic.Bridge.Core.Dispatcher
{
    // Implemented by B06 (ToolDiscovery). Called on the main thread — Unity API calls are safe.
    public interface IToolRunner
    {
        HandlerResponse Execute(HandlerRequest request);
    }
}

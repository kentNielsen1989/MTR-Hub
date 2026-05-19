using Microsoft.Xrm.Sdk;

namespace Crowe.MTRHub.Plugins
{
    public class ServiceConnection
    {
        public IPluginExecutionContext Context { get; }
        public ITracingService Trace { get; }
        public IOrganizationService Service { get; }

        public ServiceConnection(IPluginExecutionContext context,
            ITracingService trace, IOrganizationService service)
        {
            Context = context;
            Trace = trace;
            Service = service;
        }
    }
}

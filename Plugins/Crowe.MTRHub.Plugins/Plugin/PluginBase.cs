using System;
using System.Diagnostics;
using Microsoft.Xrm.Sdk;

namespace Crowe.MTRHub.Plugins
{
    public abstract class PluginBase : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)
                serviceProvider.GetService(typeof(IPluginExecutionContext));
            var tracingService = (ITracingService)
                serviceProvider.GetService(typeof(ITracingService));
            var factory = (IOrganizationServiceFactory)
                serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = factory.CreateOrganizationService(context.UserId);

            var sw = Stopwatch.StartNew();
            tracingService.Trace("[+0ms] {0} triggered: {1} (Depth={2})",
                GetType().Name, context.MessageName, context.Depth);

            try
            {
                var cnx = new ServiceConnection(context, tracingService, service);
                ExecutePlugin(cnx);
                tracingService.Trace("[+{0}ms] {1} completed successfully",
                    sw.ElapsedMilliseconds, GetType().Name);
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracingService.Trace("[+{0}ms] ERROR: {1}", sw.ElapsedMilliseconds, ex.ToString());
                throw new InvalidPluginExecutionException(
                    "An error occurred in " + GetType().Name + ". See trace logs for details.", ex);
            }
        }

        protected abstract void ExecutePlugin(ServiceConnection cnx);
    }
}

using System;
using System.Diagnostics;
using Crowe.MTRHub.Plugins.Helpers;
using Crowe.MTRHub.Plugins.Infrastructure;
using Microsoft.Xrm.Sdk;

namespace Crowe.MTRHub.Plugins
{
    public class POSearchPlugin : PluginBase
    {
        protected override void ExecutePlugin(ServiceConnection cnx)
        {
            var sw = Stopwatch.StartNew();

            string vendorNum = ReadStringInput(cnx, Literals.CustomApi.POSearch.In.VendorNum);
            if (string.IsNullOrWhiteSpace(vendorNum))
            {
                throw new InvalidPluginExecutionException(
                    "The 'VendorNum' input parameter is required and cannot be empty.");
            }

            string customerRef = ReadStringInput(cnx, Literals.CustomApi.POSearch.In.CustomerRef);
            int topN = ReadTopN(cnx);

            cnx.Trace.Trace(
                "[+{0}ms] POSearch inputs: VendorNum='{1}' CustomerRef='{2}' TopN={3}",
                sw.ElapsedMilliseconds, Mask(vendorNum), Mask(customerRef), topN);

            var container = new ServiceContainer(cnx);
            var helper = new POSearchHelper(cnx, container);
            string matchesJson = helper.FindMatches(vendorNum, customerRef, topN);

            cnx.Context.OutputParameters[Literals.CustomApi.POSearch.Out.POs] = matchesJson;
            cnx.Trace.Trace("[+{0}ms] Returning JSON payload of {1} chars.",
                sw.ElapsedMilliseconds, matchesJson.Length);
        }

        private static string ReadStringInput(ServiceConnection cnx, string key)
        {
            if (!cnx.Context.InputParameters.Contains(key)) return null;
            return cnx.Context.InputParameters[key] as string;
        }

        private static int ReadTopN(ServiceConnection cnx)
        {
            int topN = Literals.DefaultTopN;
            if (cnx.Context.InputParameters.Contains(Literals.CustomApi.POSearch.In.TopN))
            {
                var raw = cnx.Context.InputParameters[Literals.CustomApi.POSearch.In.TopN];
                if (raw is int i) topN = i;
                else if (raw != null && int.TryParse(raw.ToString(), out var parsed)) topN = parsed;
            }
            if (topN <= 0) topN = Literals.DefaultTopN;
            if (topN > Literals.MaxTopN) topN = Literals.MaxTopN;
            return topN;
        }

        private static string Mask(string s)
        {
            if (string.IsNullOrEmpty(s)) return "(null)";
            return s.Length <= 60 ? s : s.Substring(0, 60) + "…";
        }
    }
}

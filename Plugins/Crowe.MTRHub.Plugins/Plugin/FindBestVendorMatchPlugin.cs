using System;
using System.Diagnostics;
using Crowe.MTRHub.Plugins.Helpers;
using Crowe.MTRHub.Plugins.Infrastructure;
using Microsoft.Xrm.Sdk;

namespace Crowe.MTRHub.Plugins
{
    public class FindBestVendorMatchPlugin : PluginBase
    {
        protected override void ExecutePlugin(ServiceConnection cnx)
        {
            var sw = Stopwatch.StartNew();

            string name = ReadStringInput(cnx, Literals.CustomApi.In.Name);
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidPluginExecutionException(
                    "The 'Name' input parameter is required and cannot be empty.");
            }

            string address = ReadStringInput(cnx, Literals.CustomApi.In.Address);
            string zip = ReadStringInput(cnx, Literals.CustomApi.In.Zip);
            int topN = ReadTopN(cnx);

            cnx.Trace.Trace(
                "[+{0}ms] FindBestVendorMatch inputs: Name='{1}' Address='{2}' Zip='{3}' TopN={4}",
                sw.ElapsedMilliseconds,
                Mask(name), Mask(address), Mask(zip), topN);

            var container = new ServiceContainer(cnx);
            var helper = new VendorMatchHelper(cnx, container);
            string matchesJson = helper.FindMatches(name, address, zip, topN);

            cnx.Context.OutputParameters[Literals.CustomApi.Out.Vendors] = matchesJson;
            cnx.Trace.Trace("[+{0}ms] Returning JSON payload of {1} chars.",
                sw.ElapsedMilliseconds, matchesJson.Length);
        }

        private static string ReadStringInput(ServiceConnection cnx, string key)
        {
            if (!cnx.Context.InputParameters.Contains(key)) return null;
            var v = cnx.Context.InputParameters[key];
            return v as string;
        }

        private static int ReadTopN(ServiceConnection cnx)
        {
            int topN = Literals.DefaultTopN;
            if (cnx.Context.InputParameters.Contains(Literals.CustomApi.In.TopN))
            {
                var raw = cnx.Context.InputParameters[Literals.CustomApi.In.TopN];
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

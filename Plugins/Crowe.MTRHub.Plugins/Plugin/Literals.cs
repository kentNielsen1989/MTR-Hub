namespace Crowe.MTRHub.Plugins
{
    public static class Literals
    {
        public static class VendorV2
        {
            public const string EntityName = "mserp_vendvendorv2entity";
            public const string Id = "mserp_vendvendorv2entityid";
            public const string OrganizationName = "mserp_vendororganizationname";
            public const string SearchName = "mserp_vendorsearchname";
            public const string FormattedAddress = "mserp_formattedprimaryaddress";
            public const string ZipCode = "mserp_addresszipcode";
            public const string VendorAccount = "mserp_vendoraccountnumber";
        }

        public static class CustomApi
        {
            public static class In
            {
                public const string Name = "Name";
                public const string Address = "Address";
                public const string Zip = "Zip";
                public const string TopN = "TopN";
            }

            public static class Out
            {
                public const string Vendors = "Vendors";
            }

            // Property names on each JSON result object.
            public static class JsonField
            {
                public const string Rank = "rank";
                public const string VendorId = "vendorid";
                public const string VendorName = "vendorname";
                public const string VendorAccount = "vendoraccount";
                public const string Address = "address";
            }
        }

        public const int DefaultTopN = 10;
        public const int MaxTopN = 100;
        public const int MaxCandidatePullSize = 500;
    }
}

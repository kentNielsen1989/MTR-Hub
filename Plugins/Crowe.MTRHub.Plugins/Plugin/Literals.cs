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

        public static class PurchaseOrderV2
        {
            public const string EntityName = "mserp_purchpurchaseorderheaderv2entity";
            public const string Id = "mserp_purchpurchaseorderheaderv2entityid";
            public const string PurchaseOrderNumber = "mserp_purchaseordernumber";
            public const string OrderVendorAccountNumber = "mserp_ordervendoraccountnumber";
            public const string PurchaseOrderName = "mserp_purchaseordername";          // F&O label: "Vendor name"
            public const string VendorOrderReference = "mserp_vendororderreference";    // F&O label: "Customer reference"
            public const string CompanyCode = "mserp_dataareaid";
            public const string PurchaseOrderStatus = "mserp_purchaseorderstatus";

            // Active PO status values (queried from picklist metadata 2026-05-19)
            public const int StatusOpenOrder = 200000001;
            public const int StatusReceived = 200000002;
            public const int StatusInvoiced = 200000003;
        }

        public static class CustomApi
        {
            public static class FindBestVendorMatch
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

                public static class JsonField
                {
                    public const string Rank = "rank";
                    public const string VendorId = "vendorid";
                    public const string VendorName = "vendorname";
                    public const string VendorAccount = "vendoraccount";
                    public const string Address = "address";
                }
            }

            public static class POSearch
            {
                public static class In
                {
                    public const string VendorNum = "VendorNum";
                    public const string CustomerRef = "CustomerRef";
                    public const string TopN = "TopN";
                }

                public static class Out
                {
                    public const string POs = "POs";
                }

                public static class JsonField
                {
                    public const string Rank = "rank";
                    public const string POId = "poid";
                    public const string PurchaseOrderNumber = "purchaseordernumber";
                    public const string VendorAccount = "vendoraccount";
                    public const string VendorName = "vendorname";
                    public const string CustomerReference = "customerreference";
                    public const string Company = "company";
                }
            }
        }

        public const int DefaultTopN = 10;
        public const int MaxTopN = 100;
        public const int MaxCandidatePullSize = 500;
    }
}

﻿namespace Microsoft.eShopWeb.ApplicationCore.AppSettings
{
    public class AzureFunctions
    {
        public string UploadOrderDetailsUrl { get; set; }

        public bool UploadOrderDetailsIsEnabled { get; set; }

        public string UploadDeliveryDetailsUrl { get; set; }

        public bool UploadDeliveryDetailsIsEnabled { get; set; }
    }
}

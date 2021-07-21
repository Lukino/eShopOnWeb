using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Extensions.Configuration;

namespace OrderItemsReserver
{
    public static class OrderItemsReserverOld
    {
        private static readonly string _orderDetailsBlobContainerName = "order-details";

        // [FunctionName("OrderItemsReserver")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log, ExecutionContext context)
        {
            log.LogInformation($"C# Http trigger function executed at: {DateTime.Now}");

            using var streamReader = new StreamReader(req.Body);
            var orderDetails = JsonConvert.DeserializeObject<OrderDetails>(streamReader.ReadToEnd());

            var blob = (await GetCloudBlobContainer(log, context, _orderDetailsBlobContainerName)).GetBlockBlobReference($"{orderDetails.Id}.json");
            blob.Properties.ContentType = "application/json";

            using var ms = new MemoryStream();
            await blob.UploadFromStreamAsync(LoadStreamWithJson(ms, JsonConvert.SerializeObject(orderDetails)));

            log.LogInformation($"Blob {blob.Name} is uploaded to container {_orderDetailsBlobContainerName}");

            await blob.SetPropertiesAsync();

            return new OkObjectResult($"Order ('{orderDetails.Id}') details uploaded successfully!");
        }

        private async static Task<CloudBlobContainer> GetCloudBlobContainer(ILogger logger, ExecutionContext executionContext, string containerName)
        {
            var container = GetCloudStorageAccount(executionContext).CreateCloudBlobClient().GetContainerReference(containerName);
            _ = await container.CreateIfNotExistsAsync();

            return container;
        }

        private static CloudStorageAccount GetCloudStorageAccount(ExecutionContext executionContext)
        {
            var config = new ConfigurationBuilder()
                            .SetBasePath(executionContext.FunctionAppDirectory)
                            .AddJsonFile("local.settings.json", true, true)
                            .AddEnvironmentVariables()
                            .Build();

            return CloudStorageAccount.Parse(config["AzureWebJobsStorage"]);
        }

        private static Stream LoadStreamWithJson(Stream ms, object obj)
        {
            var writer = new StreamWriter(ms);

            writer.Write(obj);
            writer.Flush();
            ms.Position = 0;

            return ms;
        }

        private class OrderDetails
        {
            public string Id { get; set; }

            public decimal Quantity { get; set; }
        }
    }
}

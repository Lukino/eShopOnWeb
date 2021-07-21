using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;

namespace OrderItemsReserver
{
    public static class OrderItemsReserver
    {
        private static readonly string _orderDetailsBlobContainerName = "order-details";

        [FunctionName("OrderItemsReserver")]
        public async static void Run([ServiceBusTrigger("orderitemsreserver", Connection = "eshop-queue")] string myQueueItem, ILogger log, ExecutionContext context)
        {
            log.LogInformation($"C# Queue trigger function processed: {myQueueItem}");

            var orderDetails = JsonConvert.DeserializeObject<OrderDetails>(myQueueItem);

            var blob = (await GetCloudBlobContainer(log, context, _orderDetailsBlobContainerName)).GetBlockBlobReference($"{orderDetails.ItemId}.json");
            blob.Properties.ContentType = "application/json";

            for (var i = 0; i < 3; i++)
            {
                try
                {
                    using var ms = new MemoryStream();
                    await blob.UploadFromStreamAsync(LoadStreamWithJson(ms, JsonConvert.SerializeObject(orderDetails)));
                }
                catch
                {
                    if (i == 2)
                    {
                        throw;
                    }
                }
            }

            log.LogInformation($"Blob {blob.Name} is uploaded to container {_orderDetailsBlobContainerName}");

            await blob.SetPropertiesAsync();
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
            public string ItemId { get; set; }

            public decimal Quantity { get; set; }
        }
    }
}

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker.Extensions.ServiceBus;
using Microsoft.Azure.Cosmos;
using System.Text.Json;



namespace Company.Function;

public class OrderItemsReserver
{
    private readonly ILogger<OrderItemsReserver> _logger;

    public OrderItemsReserver(ILogger<OrderItemsReserver> logger)
    {
        _logger = logger;
    }

    [Function("OrderItemsReserver")]
    public async Task Run(
        [ServiceBusTrigger("order-reserve-requests", Connection = "ServiceBusConnection")]
        string body)
    {
        _logger.LogInformation("OrderItemsReserver received a message.");

        if (string.IsNullOrWhiteSpace(body))
        {
            _logger.LogWarning("Message body is empty.");
            return;
        }

        // Upload the JSON body to Azure Blob Storage
        var connectionString = Environment.GetEnvironmentVariable("BlobStorageConnection");
        var containerName = Environment.GetEnvironmentVariable("OrdersContainerName") ?? "order-requests";

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("BlobStorageConnection is not configured.");
        }

        var containerClient = new BlobContainerClient(connectionString, containerName);

        // safe to call even if it exists
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

        var blobName = $"order-{Guid.NewGuid():N}-{DateTime.UtcNow:yyyyMMddHHmmssfff}.json";
        var blobClient = containerClient.GetBlobClient(blobName);

        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(body));
        await blobClient.UploadAsync(ms, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" }
        });

        _logger.LogInformation("Uploaded JSON to blob {BlobName} in container {ContainerName}", blobName, containerName);
       var cosmosConnection = Environment.GetEnvironmentVariable("CosmosDbConnection");
       var cosmosDatabase = Environment.GetEnvironmentVariable("CosmosDbDatabase");
       var cosmosContainer = Environment.GetEnvironmentVariable("CosmosDbContainer");

        if (string.IsNullOrWhiteSpace(cosmosConnection))
        {
            throw new InvalidOperationException("CosmosDbConnection is not configured.");
        }

        using var cosmosClient = new CosmosClient(cosmosConnection);
        var container = cosmosClient.GetContainer(cosmosDatabase, cosmosContainer);

        var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        var orderId = root.GetProperty("orderId").GetString();

        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new InvalidOperationException("orderId is missing in the message payload.");
        }

        var cosmosItem = new
        {
            id = orderId,
            orderId = orderId,
            payload = root,
            createdAt = DateTime.UtcNow
        };

        await container.CreateItemAsync(cosmosItem, new PartitionKey(orderId));

        _logger.LogInformation("Order {OrderId} saved to Cosmos DB", orderId);


    }
}


using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.ServiceBus;


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
    }
}

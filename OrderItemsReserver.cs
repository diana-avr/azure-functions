using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;


namespace Company.Function;

public class OrderItemsReserver
{
    private readonly ILogger<OrderItemsReserver> _logger;

    public OrderItemsReserver(ILogger<OrderItemsReserver> logger)
    {
        _logger = logger;
    }

    [Function("OrderItemsReserver")]
public async Task<IActionResult> Run(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "orders/reserve")] HttpRequest req)
    {
        _logger.LogInformation("OrderItemsReserver received a request.");

    string body;
    using (var reader = new StreamReader(req.Body))
    {
        body = await reader.ReadToEndAsync();
    }

    if (string.IsNullOrWhiteSpace(body))
    {
        return new BadRequestObjectResult("Request body is empty.");
    }
    // Upload the JSON body to Azure Blob Storage
    var connectionString = Environment.GetEnvironmentVariable("BlobStorageConnection");
var containerName = Environment.GetEnvironmentVariable("OrdersContainerName") ?? "order-requests";

if (string.IsNullOrWhiteSpace(connectionString))
{
    return new ObjectResult("BlobStorageConnection is not configured.")
    {
        StatusCode = 500
    };
}

var containerClient = new BlobContainerClient(connectionString, containerName);

// you already created the container, this is just safe
await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

var blobName = $"order-{Guid.NewGuid():N}-{DateTime.UtcNow:yyyyMMddHHmmssfff}.json";
var blobClient = containerClient.GetBlobClient(blobName);

using var ms = new MemoryStream(Encoding.UTF8.GetBytes(body));
await blobClient.UploadAsync(ms, new BlobUploadOptions
{
    HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" }
});
    return new OkObjectResult(new
    {
         message = "Uploaded JSON to Blob Storage",
    container = containerName,
    blobName = blobName
    });
    }
}
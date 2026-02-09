using System;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Cosmos;


public class DeliveryOrderProcessor
{
    private readonly ILogger<DeliveryOrderProcessor> _logger;

    public DeliveryOrderProcessor(ILogger<DeliveryOrderProcessor> logger)
    {
        _logger = logger;
    }

    [Function(nameof(DeliveryOrderProcessor))]
    public async Task Run(
        [ServiceBusTrigger("myqueue", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        //  get body as string
        var body = Encoding.UTF8.GetString(message.Body);

        // YOUR COSMOS CODE (slightly wired in)
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

        var basketId = root.GetProperty("basketId").GetInt32();
        var orderId = basketId.ToString();

        var cosmosItem = new
        {
            id = orderId,
            orderId = orderId,
            basketId = basketId,
            payload = root,
            createdAt = DateTime.UtcNow
        };

        await container.CreateItemAsync(
            cosmosItem,
            new PartitionKey(orderId)
        );

        _logger.LogInformation("Order {OrderId} saved to Cosmos DB", orderId);

        //  complete message
        await messageActions.CompleteMessageAsync(message);
    }
}

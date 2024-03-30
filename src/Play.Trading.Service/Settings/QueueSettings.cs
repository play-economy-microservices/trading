namespace Play.Trading.Service.Settings;

/// <summary>
/// These are the the RabbitMQ Queue Addresses that we will send specific commands to.
/// The queues were already configured beforehand as Consumers were already added in the necessary
/// microservices.
/// <see cref="Startup.AddMassTransit(Microsoft.Extensions.DependencyInjection.IServiceCollection)"/> for queue location/mappings.
/// 
/// Example: 
/// Play.Identity.Service -> /Consumers/DebitGilConsumer.cs -> RabbitMQ does manual configuration...
///                                                                         |
///                                                      Results as identity-debit-gil (visible in portal)
/// </summary>
public class QueueSettings
{
    public string GrantItemsQueueAddress { get; init; }

    public string DebitGilQueueAddress { get; set; }
    public string SubtractItemsQueueAddress { get; init; }
}

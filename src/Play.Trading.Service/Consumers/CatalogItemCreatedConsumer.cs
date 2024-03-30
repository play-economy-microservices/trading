using System.Threading.Tasks;
using MassTransit;
using Play.Common;
using Play.Trading.Service.Entities;
using static Play.Catalog.Contracts.Contracts;

namespace Play.Trading.Service.Consumers;

public class CatalogItemCreatedConsumer : IConsumer<CatalogItemCreated>
{
    /// <summary>
    /// This is a referenece to the MongoDatabase Collection
    /// </summary>
    private readonly IRepository<CatalogItem> repository;

    public CatalogItemCreatedConsumer(IRepository<CatalogItem> repository)
    {
        this.repository = repository;
    }

    // Ensure not to have consumed this message already, if the message is there then
    // the item already exist in our CatalogItem db.(Avoid duplicates)
    public async Task Consume(ConsumeContext<CatalogItemCreated> context)
    {
        var message = context.Message;

        // Avoid duplicates
        var item = await repository.GetAsync(message.ItemId);

        if (item is not null)
        {
            return;
        }

        item = new CatalogItem()
        {
            Id = message.ItemId,
            Name = message.Name,
            Description = message.Description,
            Price = message.Price,
        };

        await repository.CreateAsync(item);
    }
}

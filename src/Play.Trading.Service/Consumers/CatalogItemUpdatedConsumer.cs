using System;
using System.Threading.Tasks;
using MassTransit;
using Play.Common;
using Play.Trading.Service.Entities;
using static Play.Catalog.Contracts.Contracts;

namespace Play.Trading.Service.Consumers;

public class CatalogItemUpdatedConsumer : IConsumer<CatalogItemUpdated>
{
    /// <summary>
    /// This is a referenece to the MongoDatabase Collection
    /// </summary>
    private readonly IRepository<CatalogItem> repository;

    public CatalogItemUpdatedConsumer(IRepository<CatalogItem> repository)
    {
        this.repository = repository;
    }

    // Find the CatalogItem if it exist. If the item is null then it doesn't exist
    // therefore, you must create the iteam. Otherwise, you can just update the CatalogItem.
    public async Task Consume(ConsumeContext<CatalogItemUpdated> context)
    {
        var message = context.Message;

        // Avoid duplicates
        var item = await repository.GetAsync(message.ItemId);

        if (item is null)
        {
            item = new CatalogItem()
            {
                Id = message.ItemId,
                Name = message.Name,
                Description = message.Description,
                Price = message.Price,
            };

            await repository.CreateAsync(item);
        }
        else
        {
            item.Name = message.Name;
            item.Description = message.Description;
            item.Price = message.Price;
            await repository.UpdateAsync(item);
        }
    }
}


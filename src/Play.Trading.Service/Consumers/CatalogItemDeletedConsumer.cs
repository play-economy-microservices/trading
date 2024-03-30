using System;
using System.Threading.Tasks;
using MassTransit;
using Play.Common;
using Play.Trading.Service.Entities;
using static Play.Catalog.Contracts.Contracts;

namespace Play.Trading.Service.Consumers;

public class CatalogItemDeletedConsumer : IConsumer<CatalogItemDeleted>
{
    /// <summary>
    /// This is a referenece to the MongoDatabase Collection
    /// </summary>
    private readonly IRepository<CatalogItem> repository;

    public CatalogItemDeletedConsumer(IRepository<CatalogItem> repository)
    {
        this.repository = repository;
    }

    // If the Catalog Item is null then it doesn't exist. Otherwise, proceed with removal.
    public async Task Consume(ConsumeContext<CatalogItemDeleted> context)
    {
        var message = context.Message;

        var item = await repository.GetAsync(message.ItemId);

        if (item is null)
        {
            return;
        }

        await repository.RemoveAsync(message.ItemId);
    }
}

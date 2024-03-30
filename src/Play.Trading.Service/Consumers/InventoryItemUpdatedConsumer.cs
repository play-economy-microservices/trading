using System.Threading.Tasks;
using MassTransit;
using Play.Common;
using Play.Inventory.Contracts;
using Play.Trading.Service.Entities;

namespace Play.Trading.Service.Consumers;

public class InventoryItemUpdatedConsumer : IConsumer<InventoryItemUpdated>
{
    /// <summary>
    /// Reference to the InventoryItem db.
    /// </summary>
    private readonly IRepository<InventoryItem> repository;

    public InventoryItemUpdatedConsumer(IRepository<InventoryItem> repository)
    {
        this.repository = repository;
    }

    public async Task Consume(ConsumeContext<InventoryItemUpdated> context)
    {
        var message = context.Message;

        // Get the item and if it's null create a new item, otherwise updated the quantity.
        var inventoryItem = await repository.GetAsync(item => item.Id == message.UserId && item.CatalogItemId == message.CatalogItemId);

        if (inventoryItem is null)
        {
            inventoryItem = new InventoryItem
            {
                CatalogItemId = message.CatalogItemId,
                UserId = message.UserId,
                Quantity = message.NewTotalQuantity
            };

            await repository.CreateAsync(inventoryItem);
        }
        else
        {
            inventoryItem.Quantity = message.NewTotalQuantity;
            await repository.UpdateAsync(inventoryItem);
        }
    }
}

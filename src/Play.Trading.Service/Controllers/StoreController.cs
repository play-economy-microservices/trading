using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Play.Common;
using Play.Trading.Service.Entities;

namespace Play.Trading.Service.Controllers;

/// <summary>
/// This is the base controller for the entire Store experience.
/// </summary>
[ApiController]
[Route("store")]
[Authorize]
public class StoreController : ControllerBase
{
    private readonly IRepository<CatalogItem> catalogRepository;

    private readonly IRepository<ApplicationUser> usersRepository;

    private readonly IRepository<InventoryItem> inventoryRepository;

    public StoreController(
        IRepository<CatalogItem> catalogRepository,
        IRepository<ApplicationUser> usersRepository,
        IRepository<InventoryItem> inventoryRepository)
    {
        this.catalogRepository = catalogRepository;
        this.usersRepository = usersRepository;
        this.inventoryRepository = inventoryRepository;
    }

    [HttpGet]
    public async Task<ActionResult<StoreDto>> GeAsync()
    {
        string userId = User.FindFirstValue("sub");

        var catalogitems = await catalogRepository.GetAllAsync();
        var inventoryitems = await inventoryRepository.GetAllAsync(item => item.UserId == Guid.Parse(userId));
        var user = await usersRepository.GetAsync(Guid.Parse(userId));

        // Construct model that will be presented to the user (i.e store experience)
        var storeDto = new StoreDto(
            catalogitems.Select(catalogitem =>
                new StoreItemDto(
                    catalogitem.Id,
                    catalogitem.Name,
                    catalogitem.Description,
                    catalogitem.Price,
                    inventoryitems.FirstOrDefault( // how much a user owns of the item
                        inventoryitem => inventoryitem.CatalogItemId == catalogitem.Id)?.Quantity ?? 0
                    )
                ),
                user?.Gil ?? 0
        );

        return Ok(storeDto);
    }
}

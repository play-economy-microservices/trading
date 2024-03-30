using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Play.Trading.Service;

/// <summary>
/// Record that describes the POST request for a Purchase.
/// </summary>
public record SubmitPurchaseDto(
    [Required] Guid? ItemId,
    [Range(1, 100)] int Quantity,
    [Required] Guid? IdempotencyId
);

/// <summary>
/// Record that describes the GET request for the Purchase.
/// </summary>
public record PurchaseDto(
    Guid UserId,
    Guid ItemId,
    decimal? PurchaseTotal,
    int Quantity,
    string State,
    string Reason,
    DateTimeOffset Received,
    DateTimeOffset LastUpdated
);

/// <summary>
/// Represents an item that will presented in the store.
/// </summary>
public record StoreItemDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    int OwnedQuantity
);

/// <summary>
/// Represents the wallet of the user in the store.
/// </summary>
public record StoreDto(IEnumerable<StoreItemDto> Items, decimal UserGil);
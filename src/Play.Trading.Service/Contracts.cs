using System;

namespace Play.Trading.Service;

/// <summary>
/// These records will only be used within Trading service.
/// </summary>
public record PurchaseRequested(
    Guid UserId,
    Guid ItemId,
    int Quantity,
    Guid CorrelationId);

public record GetPurchaseState(Guid CorrelationId);
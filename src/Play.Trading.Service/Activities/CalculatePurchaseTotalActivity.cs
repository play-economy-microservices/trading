using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Automatonymous;
using GreenPipes;
using Play.Common;
using Play.Trading.Service.Entities;
using Play.Trading.Service.Exceptions;
using Play.Trading.Service.StateMachines;

namespace Play.Trading.Service.Activities;

/// <summary>
/// Activity that will be activated when receiving PurchaseRequested event.
/// </summary>
public class CalculatePurchaseTotalActivity : Activity<PurchaseState, PurchaseRequested>
{
    private readonly IRepository<CatalogItem> repository;

    public CalculatePurchaseTotalActivity(IRepository<CatalogItem> repository)
    {
        this.repository = repository;
    }

    public void Accept(StateMachineVisitor visitor)
    {
        visitor.Visit(this);
    }

    public async Task Execute(BehaviorContext<PurchaseState, PurchaseRequested> context, Behavior<PurchaseState, PurchaseRequested> next)
    {
        // Context.Data = content that arrived to the state machine before invoking the activity.
        var message = context.Data;

        var item = await repository.GetAsync(message.ItemId);

        if (item is null)
        {
            throw new UnknownItemException(message.ItemId);
        }

        context.Instance.PurchaseTotal = item.Price * message.Quantity;
        context.Instance.LastUpdated = DateTimeOffset.UtcNow;

        // continue moving on the pipeline
        await next.Execute(context).ConfigureAwait(false);
    }

    public Task Faulted<TException>(BehaviorExceptionContext<PurchaseState, PurchaseRequested, TException> context, Behavior<PurchaseState, PurchaseRequested> next) where TException : Exception
    {
        return next.Faulted(context);
    }

    public void Probe(ProbeContext context)
    {
        // Some string that represents this activity...
        context.CreateScope("calculate-purchase-total");
    }
}
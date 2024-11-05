using System;
using Automatonymous;
using MassTransit;
using Play.Identity.Contracts;
using Play.Inventory.Contracts;
using Play.Trading.Service.Activities;
using Play.Trading.Service.Contracts;
using Play.Trading.Service.SignalR;

namespace Play.Trading.Service.StateMachines
{
    /// <summary>
    /// This class is the actual concrete State Machine.
    /// </summary>
    public class PurchaseStateMachine : MassTransitStateMachine<PurchaseState>
    {
        private readonly MessageHub hub;

        public State Accepted { get; }
        public State ItemsGranted { get; }
        public State Completed { get; }
        public State Faulted { get; }

        public Event<PurchaseRequested> PurchaseRequested { get; }
        public Event<GetPurchaseState> GetPurchaseState { get; }
        public Event<InventoryItemsGranted> InventoryItemsGranted { get; }
        public Event<GilDebited> GilDebited { get; }
        
        // Event with other services fails doing the operations (will be used)
        public Event<Fault<GrantItems>> GrantItemsFaulted { get; }
        public Event<Fault<DebitGil>> DebitGilFaulted { get; }

        public PurchaseStateMachine(MessageHub hub)
        {
            // Track the state
            InstanceState(state => state.CurrentState);
            ConfigureEvents();
            ConfigureInitialState();
            ConfigureAny();
            ConfigureAccepted();
            ConfigureItemsGranted();
            ConfigureFaulted();
            ConfigureCompleted();
            this.hub = hub;
        }

        // MassTransit will make use of any necessary events in this method.
        private void ConfigureEvents()
        {
            Event(() => PurchaseRequested);
            Event(() => GetPurchaseState);
            Event(() => InventoryItemsGranted);
            Event(() => GilDebited);
            Event(() => GrantItemsFaulted, x => x.CorrelateById(
                context => context.Message.Message.CorrelationId));
            Event(() => DebitGilFaulted, x => x.CorrelateById(
                context => context.Message.Message.CorrelationId));
        }
        
        private void ConfigureInitialState()
        {
            // When receiving a PurchaseRequested event, then we must obtain all of these properties.
            Initially(
                When(PurchaseRequested) // 1. Obtain the props
                    .Then(context =>
                    {
                        // For clarification, instance = this class instance and Data = Incoming Request (i.e PurchaseRequested)
                        context.Instance.UserId = context.Data.UserId;
                        context.Instance.ItemId = context.Data.ItemId;
                        context.Instance.Quantity = context.Data.Quantity;
                        context.Instance.Received = DateTimeOffset.UtcNow;
                        context.Instance.LastUpdated = context.Instance.Received;
                    })
                    .Activity(x => x.OfType<CalculatePurchaseTotalActivity>()) // 2. Perform the calculation
                    .Send(context => new GrantItems( // 3. Send the GrantItems request to Inventory microservice
                        context.Instance.UserId,
                        context.Instance.ItemId,
                        context.Instance.Quantity,
                        context.Instance.CorrelationId))
                    .TransitionTo(Accepted) // 4. Happy Case!
                    .Catch<Exception>(ex => ex.
                        Then(context =>
                        {
                            // Store the exception (if any) into the instance state
                            context.Instance.ErrorMessage = context.Exception.Message;
                            context.Instance.LastUpdated = DateTimeOffset.UtcNow;
                        })
                        .TransitionTo(Faulted)
                        .ThenAsync(async context => await hub.SendStatusAsync(context.Instance))) // let the client know
            );
        }

        private void ConfigureAccepted()
        {
            // During the accepted state, 
            During(Accepted,
                Ignore(PurchaseRequested), // Already moved forward, no need to accept this request
                When(InventoryItemsGranted) // Inventory sends this back
                    .Then(context =>
                    {
                        context.Instance.LastUpdated = DateTimeOffset.UtcNow;
                    })
                    .Send(context => new DebitGil( // Send to Identity service
                        context.Instance.UserId,
                        context.Instance.PurchaseTotal.Value,
                        context.Instance.CorrelationId
                    ))
                    .TransitionTo(ItemsGranted),
                When(GrantItemsFaulted) // This section is for when things fail in some other service
                    .Then(context =>
                    {
                        context.Instance.ErrorMessage = context.Data.Exceptions[0].Message;
                        context.Instance.LastUpdated = DateTimeOffset.UtcNow;
                    })
                    .TransitionTo(Faulted)
                    .ThenAsync(async context => await hub.SendStatusAsync(context.Instance)) // let the client know
                );
        }

        private void ConfigureItemsGranted()
        {
            During(ItemsGranted, 
                Ignore(PurchaseRequested), // Already moved forward, no need to accept this request
                Ignore(InventoryItemsGranted), // Already moved forward, no need to accept this request
                When(GilDebited) // Identity sends this event back
                    .Then(context =>
                    {
                        context.Instance.LastUpdated = DateTimeOffset.UtcNow;
                    })
                    .TransitionTo(Completed) // Once Identity sends GilDebit successfully = Done!
                    .ThenAsync(async context => await hub.SendStatusAsync(context.Instance)), // let the client know
                When(DebitGilFaulted) // If something goes wrong, we must revert/tell the service to subtract
                    .Send(context => new SubtractItems( // Inventory will handle this
                        context.Instance.UserId,
                        context.Instance.ItemId,
                        context.Instance.Quantity,
                        context.Instance.CorrelationId
                    ))
                    .Then(context =>
                    {
                        context.Instance.ErrorMessage = context.Data.Exceptions[0].Message;
                        context.Instance.LastUpdated = DateTimeOffset.UtcNow;
                    })
                    .TransitionTo(Faulted)
                    .ThenAsync(async context => await hub.SendStatusAsync(context.Instance)) // let the client know
            );
        }

        private void ConfigureCompleted()
        {
            // Already Completed Ignore any other request.
            During(Completed,
                Ignore(PurchaseRequested),
                Ignore(InventoryItemsGranted),
                Ignore(GilDebited)
            );
        }

        private void ConfigureAny()
        {
            DuringAny(
                When(GetPurchaseState)
                    .Respond(x => x.Instance)
            );
        }

        private void ConfigureFaulted()
        {
            // If there's an error, ignore any of these events
            During(Faulted,
                Ignore(PurchaseRequested),
                Ignore(InventoryItemsGranted),
                Ignore(GilDebited)
            );
        }
    }
}
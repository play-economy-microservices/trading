using System;
using MassTransit;
using Microsoft.Extensions.Logging;
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

        private readonly ILogger<PurchaseStateMachine> logger;

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

        public PurchaseStateMachine(MessageHub hub, ILogger<PurchaseStateMachine> logger)
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
            this.logger = logger;
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
                        context.Saga.UserId = context.Message.UserId;
                        context.Saga.ItemId = context.Message.ItemId;
                        context.Saga.Quantity = context.Message.Quantity;
                        context.Saga.Received = DateTimeOffset.UtcNow;
                        context.Saga.LastUpdated = context.Saga.Received;
                        
                        logger.LogInformation(
                            "Calculating total price for purchase with CorrelationId {CorrelationId}...",
                            context.Saga.CorrelationId);
                    })
                    .Activity(x => x.OfType<CalculatePurchaseTotalActivity>()) // 2. Perform the calculation
                    .Send(context => new GrantItems( // 3. Send the GrantItems request to Inventory microservice
                        context.Saga.UserId,
                        context.Saga.ItemId,
                        context.Saga.Quantity,
                        context.Saga.CorrelationId))
                    .TransitionTo(Accepted) // 4. Happy Case!
                    .Catch<Exception>(ex => ex.
                        Then(context =>
                        {
                            // Store the exception (if any) into the instance state
                            context.Saga.ErrorMessage = context.Exception.Message;
                            context.Saga.LastUpdated = DateTimeOffset.UtcNow;
                            
                            logger.LogError(
                                context.Exception, 
                                "Could not calculate the total price of purchase with CorrelationId {CorrelationId}, Error: {ErrorMessage}",
                                context.Saga.CorrelationId, context.Saga.ErrorMessage);
                        })
                        .TransitionTo(Faulted)
                        .ThenAsync(async context => await hub.SendStatusAsync(context.Saga))) // let the client know
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
                        context.Saga.LastUpdated = DateTimeOffset.UtcNow;
                        
                        logger.LogInformation(
                            "Items of purchase with CorrelationId {CorrelationId} have been granted to user {UserId}", 
                            context.Saga.CorrelationId, 
                            context.Saga.UserId);
                    })
                    .Send(context => new DebitGil( // Send to Identity service
                        context.Saga.UserId,
                        context.Saga.PurchaseTotal.Value,
                        context.Saga.CorrelationId
                    ))
                    .TransitionTo(ItemsGranted),
                When(GrantItemsFaulted) // This section is for when things fail in some other service
                    .Then(context =>
                    {
                        context.Saga.ErrorMessage = context.Message.Exceptions[0].Message;
                        context.Saga.LastUpdated = DateTimeOffset.UtcNow;
                        
                        logger.LogError(
                            "Could not grant items for purchase with CorrelationId {CorrelationId}, Error: {ErrorMEssage}", 
                            context.Saga.CorrelationId, 
                            context.Saga.ErrorMessage);
                    })
                    .TransitionTo(Faulted)
                    .ThenAsync(async context => await hub.SendStatusAsync(context.Saga)) // let the client know
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
                        context.Saga.LastUpdated = DateTimeOffset.UtcNow;
                        
                        logger.LogInformation(
                            "The total price of purchase with CorrelationId {CorrelationId} has been debited from user {UserId}. Purchase Complete",
                            context.Saga.CorrelationId,
                            context.Saga.UserId);
                    })
                    .TransitionTo(Completed) // Once Identity sends GilDebit successfully = Done!
                    .ThenAsync(async context => await hub.SendStatusAsync(context.Saga)), // let the client know
                When(DebitGilFaulted) // If something goes wrong, we must revert/tell the service to subtract
                    .Send(context => new SubtractItems( // Inventory will handle this
                        context.Saga.UserId,
                        context.Saga.ItemId,
                        context.Saga.Quantity,
                        context.Saga.CorrelationId
                    ))
                    .Then(context =>
                    {
                        context.Saga.ErrorMessage = context.Message.Exceptions[0].Message;
                        context.Saga.LastUpdated = DateTimeOffset.UtcNow;
                        
                        logger.LogError("Could not debit the total price of purchase with CorrelationId {CorrelationId} from user {UserId}, Error: {ErrorMessage}",
                            context.Saga.CorrelationId,
                            context.Saga.UserId,
                            context.Saga.ErrorMessage);
                    })
                    .TransitionTo(Faulted)
                    .ThenAsync(async context => await hub.SendStatusAsync(context.Saga)) // let the client know
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
                    .Respond(x => x.Saga)
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
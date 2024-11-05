using System;
using Automatonymous;
using MassTransit.Saga;

namespace Play.Trading.Service.StateMachines
{
    public class PurchaseState : SagaStateMachineInstance, ISagaVersion
    {
        public Guid CorrelationId { get; set; }
        
        /// <summary>
        /// Current state of the State Machine (i.e accepted, completed, etc)
        /// </summary>
        public string CurrentState { get; set; }
        
        /// <summary>
        /// Who is triggering this request.
        /// </summary>
        public Guid UserId { get; set; }
        
        /// <summary>
        /// The item that will be purchased.
        /// </summary>
        public Guid ItemId { get; set; }
        
        /// <summary>
        /// Price of the item we're trying to purchase.
        /// </summary>
        public int Quantity { get; set; }
        
        /// <summary>
        /// Keep track of when received the request.
        /// </summary>
        public DateTimeOffset Received { get; set; }
        
        /// <summary>
        /// Total amount of items
        /// </summary>
        public decimal? PurchaseTotal { get; set; }
        
        /// <summary>
        /// Track last time State Machine received an update.
        /// </summary>
        public DateTimeOffset LastUpdated { get; set; }
        
        /// <summary>
        /// Track error message (if any) when accessing State Machine.
        /// </summary>
        public string ErrorMessage { get; set; }
        
        /// <summary>
        /// Track current State Machine Version.
        /// </summary>
        public int Version { get; set; }
    }
}
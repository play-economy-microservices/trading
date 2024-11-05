using System;
using Play.Common;

namespace Play.Trading.Service.Entities
{
    /// <summary>
    /// Model for the Trading Service Db
    /// </summary>
    public class CatalogItem : IEntity
    {
        /// <summary>
        /// Id of the Catalog Item.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Name of the Catalog Item
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Description of the Catalog Item
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Price of the Catalog Item
        /// </summary>
        public decimal Price { get; set; }
    }
}
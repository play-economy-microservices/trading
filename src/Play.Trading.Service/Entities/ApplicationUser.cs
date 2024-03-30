using System;
using Play.Common;

namespace Play.Trading.Service.Entities;

/// <summary>
/// Model for the Trading Service Db
/// </summary>
public class ApplicationUser : IEntity
{
    public Guid Id { get; set; }

    public decimal Gil { get; set; }
}
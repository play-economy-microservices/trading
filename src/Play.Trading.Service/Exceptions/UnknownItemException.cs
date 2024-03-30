using System;
using System.Runtime.Serialization;

namespace Play.Trading.Service.Exceptions;

[Serializable]
internal class UnknownItemException : Exception
{
    public UnknownItemException(Guid ItemId) : base($"Unknown item '{ItemId}'")
    {
        this.ItemId = ItemId;
    }

    public Guid ItemId { get; }
}
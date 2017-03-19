using System;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// Handles events related to lists of AMQP exchanges.
    /// </summary>
    /// <param name="exchangeList">The list of exchanges.</param>
    public delegate void ExchangeListEventHandler(AmqpExchange[] exchangeList);
}

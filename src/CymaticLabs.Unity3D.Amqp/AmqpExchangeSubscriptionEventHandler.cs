using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// Handles events related to exchange subscriptions.
    /// </summary>
    /// <param name="subscription">The subscription that was the target of the event.</param>
    public delegate void AmqpExchangeSubscriptionEventHandler(AmqpExchangeSubscription subscription);
}

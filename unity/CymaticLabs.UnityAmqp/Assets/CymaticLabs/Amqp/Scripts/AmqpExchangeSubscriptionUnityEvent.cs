using System;
using UnityEngine.Events;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// A Unity-specific event for exchange-based subscriptions.
    /// </summary>
    [Serializable]
    public class AmqpExchangeSubscriptionUnityEvent : UnityEvent<AmqpExchangeSubscription>
    {
    }
}

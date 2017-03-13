using System;
using UnityEngine.Events;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// A Unity-specific message received event for exchange-based subscriptions.
    /// </summary>
    [Serializable]
    public class AmqpExchangeMessageReceivedUnityEvent : UnityEvent<AmqpExchangeSubscription, IAmqpReceivedMessage>
    {
    }
}

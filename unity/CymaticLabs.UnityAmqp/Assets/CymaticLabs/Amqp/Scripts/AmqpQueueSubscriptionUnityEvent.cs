using System;
using UnityEngine.Events;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// A Unity-specific event for queue-based subscriptions.
    /// </summary>
    [Serializable]
    public class AmqpQueueSubscriptionUnityEvent : UnityEvent<AmqpQueueSubscription>
    {
    }
}

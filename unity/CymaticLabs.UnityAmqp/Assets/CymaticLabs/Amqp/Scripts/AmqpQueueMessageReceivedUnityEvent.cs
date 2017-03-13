using System;
using UnityEngine.Events;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// A Unity-specific message received event for queue-based subscriptions.
    /// </summary>
    [Serializable]
    public class AmqpQueueMessageReceivedUnityEvent : UnityEvent<AmqpQueueSubscription, IAmqpReceivedMessage>
    {
    }
}

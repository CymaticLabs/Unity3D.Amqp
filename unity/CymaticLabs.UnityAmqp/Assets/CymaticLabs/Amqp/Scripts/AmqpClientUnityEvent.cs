using System;
using UnityEngine.Events;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// A Unity event for <see cref="AmqpClient"/> instances.
    /// </summary>
    [Serializable]
    public class AmqpClientUnityEvent : UnityEvent<AmqpClient>
    {
    }
}

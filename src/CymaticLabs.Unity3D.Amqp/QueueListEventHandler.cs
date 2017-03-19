using System;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// Handles events related to lists of AMQP queues.
    /// </summary>
    /// <param name="queueList">The list of queues.</param>
    public delegate void QueueListEventHandler(AmqpQueue[] queueList);
}

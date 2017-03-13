namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// Handles AMQP client message received events.
    /// </summary>
    /// <param name="connection">The broker connection the message was received from.</param>
    /// <param name="received">The underlying received message data.</param>
    public delegate void AmqpQueueMessageReceivedEventHandler(AmqpQueueReceivedMessage received);
}

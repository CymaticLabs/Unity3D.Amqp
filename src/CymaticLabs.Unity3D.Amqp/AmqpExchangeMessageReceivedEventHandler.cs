namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// Handles AMQP client message received events.
    /// </summary>
    /// <param name="received">The underlying received message data.</param>
    public delegate void AmqpExchangeMessageReceivedEventHandler(AmqpExchangeReceivedMessage received);
}

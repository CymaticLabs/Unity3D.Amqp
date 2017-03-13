namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// Contains the information and data for a message received from an AMQP broker.
    /// </summary>
    public interface IAmqpReceivedMessage
    {
        #region Properties

        /// <summary>
        /// The message body/payload.
        /// </summary>
        byte[] Body { get; }

        /// <summary>
        /// The consumer tag of the consumer the message was delivered to.
        /// </summary>
        string ConsumerTag { get; }

        /// <summary>
        /// The delivery tag for this delivery.
        /// </summary>
        ulong DeliveryTag { get; }

        /// <summary>
        /// The name of the exchange the message was originally published to.
        /// </summary>
        string Exchange { get; }

        /// <summary>
        /// The message properties which includes things like headers, content type, delivery mode, etc.
        /// </summary>
        IAmqpMessageProperties Properties { get; }

        /// <summary>
        /// The AMQP "redelivered" flag.
        /// </summary>
        bool Redelivered { get; }

        /// <summary>
        /// The routing key used when the message was originally published.
        /// </summary>
        string RoutingKey { get; }

        #endregion Properties
    }
}

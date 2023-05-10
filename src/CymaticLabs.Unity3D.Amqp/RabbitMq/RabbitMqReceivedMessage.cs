using System;
using RabbitMQ.Client.Events;

namespace CymaticLabs.Unity3D.Amqp.RabbitMq
{
    /// <summary>
    /// Wraps an <see cref="RabbitMQ.Client.Events.BasicDeliverEventArgs">RabbitMQ basic delivery event object</see>
    /// in an <see cref="IAmqpReceivedMessage"/> interface.
    /// </summary>
    public class RabbitMqReceivedMessage : IAmqpReceivedMessage
    {
        #region Fields

        // The internal RabbitMQ basic delivery event args reference being wrapped.
        BasicDeliverEventArgs wrapped;

        // The internal RabbitMQ wrapped basic properties
        RabbitMqMessageProperties properties;

        #endregion Fields

        #region Properties

        /// <summary>
        /// The message body/payload.
        /// </summary>
        public byte[] Body
        {
            get
            {
                return wrapped.Body;
            }
        }

        /// <summary>
        /// The consumer tag of the consumer the message was delivered to.
        /// </summary>
        public string ConsumerTag
        {
            get
            {
                return wrapped.ConsumerTag;
            }
        }

        /// <summary>
        /// The delivery tag for this delivery.
        /// </summary>
        public ulong DeliveryTag
        {
            get
            {
                return wrapped.DeliveryTag;
            }
        }

        /// <summary>
        /// The name of the exchange the message was originally published to.
        /// </summary>
        public string Exchange
        {
            get
            {
                return wrapped.Exchange;
            }
        }

        /// <summary>
        /// The message properties which includes things like headers, content type, delivery mode, etc.
        /// </summary>
        public IAmqpMessageProperties Properties
        {
            get
            {
                return properties;
            }
        }

        /// <summary>
        /// The AMQP "redelivered" flag.
        /// </summary>
        public bool Redelivered
        {
            get
            {
                return wrapped.Redelivered;
            }
        }

        /// <summary>
        /// The routing key used when the message was originally published.
        /// </summary>
        public string RoutingKey
        {
            get
            {
                return wrapped.RoutingKey;
            }
        }

        #endregion Properties

        #region Constructors

        /// <summary>
        /// Creates a new instance wrapping the supplied <see cref="RabbitMQ.Client.Events.BasicDeliverEventArgs"/> instance.
        /// </summary>
        /// <param name="basicDeliverEventArgs">The RabbitMQ basic deliver args to wrap.</param>
        public RabbitMqReceivedMessage(BasicDeliverEventArgs basicDeliverEventArgs)
        {
            if (basicDeliverEventArgs == null) throw new ArgumentNullException("basicDeliverEventArgs");
            wrapped = basicDeliverEventArgs;
            properties = new RabbitMqMessageProperties(wrapped.BasicProperties);
        }

        #endregion Constructors

        #region Methods

        #endregion Methods
    }
}

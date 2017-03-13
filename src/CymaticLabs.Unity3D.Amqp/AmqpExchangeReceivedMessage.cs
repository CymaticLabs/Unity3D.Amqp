using System;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// Represents a message received from a specific exchange subscription.
    /// </summary>
    public class AmqpExchangeReceivedMessage
    {
        /// <summary>
        /// The <see cref="AmqpExchangeSubscription">subscription</see> that the message was received from.
        /// </summary>
        public AmqpExchangeSubscription Subscription { get; private set; }

        /// <summary>
        /// The <see cref="IAmqpReceivedMessage">message</see> that was received.
        /// </summary>
        public IAmqpReceivedMessage Message { get; private set; }

        // Constructor
        public AmqpExchangeReceivedMessage(AmqpExchangeSubscription subscription, IAmqpReceivedMessage message)
        {
            if (subscription == null) throw new ArgumentNullException("subscription");
            if (message == null) throw new ArgumentNullException("message");

            Subscription = subscription;
            Message = message;
        }
    }
}

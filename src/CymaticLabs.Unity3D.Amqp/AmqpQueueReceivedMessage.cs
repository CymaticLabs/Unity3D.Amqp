using System;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// Represents a message received from a specific queue subscription.
    /// </summary>
    public class AmqpQueueReceivedMessage
    {
        /// <summary>
        /// The <see cref="AmqpQueueSubscription">subscription</see> that the message was received from.
        /// </summary>
        public AmqpQueueSubscription Subscription { get; private set; }

        /// <summary>
        /// The <see cref="IAmqpReceivedMessage">message</see> that was received.
        /// </summary>
        public IAmqpReceivedMessage Message { get; private set; }

        // Constructor
        public AmqpQueueReceivedMessage(AmqpQueueSubscription subscription, IAmqpReceivedMessage message)
        {
            if (subscription == null) throw new ArgumentNullException("subscription");
            if (message == null) throw new ArgumentNullException("message");

            Subscription = subscription;
            Message = message;
        }
    }
}

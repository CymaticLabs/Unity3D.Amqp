using System;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// Stores the state of a queue subscription on a broker connection.
    /// </summary>
    [Serializable]
    public class AmqpQueueSubscription : AmqpSubscriptionBase
    {
        #region Properties

        /// <summary>
        /// The exchange/topic that the subscription is for.
        /// </summary>
        public string QueueName;

        /// <summary>
        /// The subscription event handler to use to handle message received from the subscription.
        /// </summary>
        public AmqpQueueMessageReceivedEventHandler Handler;

        /// <summary>
        /// <param name="handler">The subscription event handler to use to handle message received from the subscription when the message
        /// should be handled on a different thread than the receiving message thread. This technique is a must for use with Unity's game thread.</param>
        /// </summary>
        public AmqpQueueMessageReceivedEventHandler ThreadsafeHandler;

        /// <summary>
        /// Whether or not to use message acknowledgement when consuming from the queue.
        /// </summary>
        public bool UseAck;

        #endregion Properties

        #region Constructor

        /// <summary>
        /// Empty constructor.
        /// </summary>
        public AmqpQueueSubscription()
        {
        }

        /// <summary>
        /// Creates a new exchange subscription.
        /// </summary>
        /// <param name="queueName">The name of the queue to subscribe to.</param>
        /// <param name="useAck">Whether or not to use message acknowledgement when consuming from the queue.</param>
        /// <param name="handler">The message received handler to use with the subscription.</param>
        public AmqpQueueSubscription(string queueName, bool useAck, AmqpQueueMessageReceivedEventHandler handler)
            : this("Queue Subscription", queueName, useAck, handler)
        { }

        /// <summary>
        /// Creates a new exchange subscription.
        /// </summary>
        /// <param name="name">The name to give the subscription.</param>
        /// <param name="queueName">The name of the queue to subscribe to.</param>
        /// <param name="useAck">Whether or not to use message acknowledgement when consuming from the queue.</param>
        /// <param name="handler">The message received handler to use with the subscription.</param>
        public AmqpQueueSubscription(string name, string queueName, bool useAck, AmqpQueueMessageReceivedEventHandler handler)
        {
            if (string.IsNullOrEmpty(queueName)) throw new ArgumentNullException("queueName");

            Name = name;
            QueueName = queueName;
            UseAck = useAck;
            Handler = handler;
        }

        #endregion Constructor
    }
}

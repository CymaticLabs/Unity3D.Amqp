using System;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// Stores the state of an exchange subscription on a broker connection.
    /// </summary>
    [Serializable]
    public class AmqpExchangeSubscription : AmqpSubscriptionBase
    {
        #region Properties

        /// <summary>
        /// The name of the exchange to subscribe to.
        /// </summary>
        public string ExchangeName;

        /// <summary>
        /// The type of the exchange being subscribed to.
        /// </summary>
        public AmqpExchangeTypes ExchangeType;

        /// <summary>
        /// <param name="handler">The subscription event handler to use to handle message received from the subscription.</param>
        /// </summary>
        public AmqpExchangeMessageReceivedEventHandler Handler;

        /// <summary>
        /// <param name="handler">The subscription event handler to use to handle message received from the subscription when the message
        /// should be handled on a different thread than the receiving message thread. This technique is a must for use with Unity's game thread.</param>
        /// </summary>
        public AmqpExchangeMessageReceivedEventHandler ThreadsafeHandler;

        /// <summary>
        /// The subscription routing key (if any).
        /// </summary>
        public string RoutingKey;

        #endregion Properties

        #region Constructor

        /// <summary>
        /// Empty constructor.
        /// </summary>
        public AmqpExchangeSubscription()
        {
            Enabled = true; // default to enabled
            RoutingKey = "";
        }

        /// <summary>
        /// Creates a new exchange subscription.
        /// </summary>
        /// <param name="exchangeName">The name of the exchange to subscribe to.</param>
        /// <param name="exchangeType">The type of exchange to subscribe to.</param>
        /// <param name="handler">The message received handler to use with the subscription.</param>
        public AmqpExchangeSubscription(string exchangeName, AmqpExchangeTypes exchangeType, AmqpExchangeMessageReceivedEventHandler handler)
            : this("Exchange Subscription", exchangeName, exchangeType, "", handler)
        { }

        /// <summary>
        /// Creates a new exchange subscription.
        /// </summary>
        /// <param name="exchangeName">The name of the exchange to subscribe to.</param>
        /// <param name="exchangeType">The type of exchange to subscribe to.</param>
        /// <param name="routingKey">The exchange routing key if any.</param>
        /// <param name="handler">The message received handler to use with the subscription.</param>
        public AmqpExchangeSubscription(string exchangeName, AmqpExchangeTypes exchangeType, string routingKey, AmqpExchangeMessageReceivedEventHandler handler)
            : this("Exchange Subscription", exchangeName, exchangeType, routingKey, handler)
        { }

        /// <summary>
        /// Creates a new exchange subscription.
        /// </summary>
        /// <param name="name">The name to give the subscription.</param>
        /// <param name="exchangeName">The name of the exchange to subscribe to.</param>
        /// <param name="exchangeType">The type of exchange to subscribe to.</param>
        /// <param name="routingKey">The exchange routing key if any.</param>
        /// <param name="handler">The message received handler to use with the subscription.</param>
        public AmqpExchangeSubscription(string name, string exchangeName, 
            AmqpExchangeTypes exchangeType, string routingKey, AmqpExchangeMessageReceivedEventHandler handler)
        {
            if (string.IsNullOrEmpty(exchangeName)) throw new ArgumentNullException("exchangeName");

            Name = name;
            Enabled = true; // default to enabled
            ExchangeName = exchangeName;
            ExchangeType = exchangeType;
            Handler = handler;
            RoutingKey = routingKey != null ? routingKey : ""; // routing key cannot be null
        }

        #endregion Constructor
    }
}

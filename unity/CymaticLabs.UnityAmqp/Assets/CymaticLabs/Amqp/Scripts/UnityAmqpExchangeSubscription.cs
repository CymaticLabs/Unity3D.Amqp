using UnityEngine.Events;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// Unity-specific AMQP exchange subscription that exposes Unity events for the
    /// subscription's message received handler.
    /// </summary>
    [System.Serializable]
    public class UnityAmqpExchangeSubscription : AmqpExchangeSubscription
    {
        /// <summary>
        /// Occurs when a message is received by the subscription.
        /// </summary>
        public AmqpExchangeMessageReceivedUnityEvent OnMessageReceived;

        /// <summary>
        /// Creates a new exchange subscription.
        /// </summary>
        /// <param name="exchangeName">The name of the exchange to subscribe to.</param>
        /// <param name="exchangeType">The type of exchange to subscribe to.</param>
        /// <param name="handler">The message received handler to use with the subscription.</param>
        /// <param name="unityHandler">The Unity message received handler to use with the subscription.</param>
        public UnityAmqpExchangeSubscription(string exchangeName, AmqpExchangeTypes exchangeType, 
            AmqpExchangeMessageReceivedEventHandler handler, UnityAction<AmqpExchangeSubscription, IAmqpReceivedMessage> unityHandler)
            : this("Unity Exchange Subscription", exchangeName, exchangeType, "", handler, unityHandler)
        { }

        /// <summary>
        /// Creates a new exchange subscription.
        /// </summary>
        /// <param name="exchangeName">The name of the exchange to subscribe to.</param>
        /// <param name="exchangeType">The type of exchange to subscribe to.</param>
        /// <param name="routingKey">The exchange routing key if any.</param>
        /// <param name="handler">The message received handler to use with the subscription.</param>
        /// <param name="unityHandler">The Unity message received handler to use with the subscription.</param>
        public UnityAmqpExchangeSubscription(string exchangeName, AmqpExchangeTypes exchangeType, string routingKey, 
            AmqpExchangeMessageReceivedEventHandler handler, UnityAction<AmqpExchangeSubscription, IAmqpReceivedMessage> unityHandler)
            : this("Unity Exchange Subscription", exchangeName, exchangeType, routingKey, handler, unityHandler)
        { }

        /// <summary>
        /// Creates a new exchange subscription.
        /// </summary>
        /// <param name="name">The name to give the subscription.</param>
        /// <param name="exchangeName">The name of the exchange to subscribe to.</param>
        /// <param name="exchangeType">The type of exchange to subscribe to.</param>
        /// <param name="routingKey">The exchange routing key if any.</param>
        /// <param name="handler">The message received handler to use with the subscription.</param>
        public UnityAmqpExchangeSubscription(string name, string exchangeName,
            AmqpExchangeTypes exchangeType, string routingKey, AmqpExchangeMessageReceivedEventHandler handler, UnityAction<AmqpExchangeSubscription, IAmqpReceivedMessage> unityHandler)
            : base(name, exchangeName, exchangeType, routingKey, handler)
        {
            OnMessageReceived = new AmqpExchangeMessageReceivedUnityEvent();
            OnMessageReceived.AddListener(unityHandler);
        }
    }
}

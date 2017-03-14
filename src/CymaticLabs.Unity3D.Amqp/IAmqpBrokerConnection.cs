using System;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// Interface that degines connection information relating to an AMQP broker.
    /// </summary>
    public interface IAmqpBrokerConnection
    {
        #region Properties

        /// <summary>
        /// The name or IP of the host broker.
        /// </summary>
        string Server { get; }

        /// <summary>
        /// The host broker's AMQP port number.
        /// </summary>
        int AmqpPort { get; }

        /// <summary>
        /// The host broker's web/REST API port number.
        /// </summary>
        int WebPort { get; }

        /// <summary>
        /// The broker vhost to use. Default is '/'.
        /// </summary>
        string VirtualHost { get; }

        /// <summary>
        /// The username for the client connection.
        /// </summary>
        string Username { get; }

        /// <summary>
        /// The password for the client connection.
        /// </summary>
        string Password { get; }

        /// <summary>
        /// Gets the number of seconds between reconnection attempts.
        /// </summary>
        short ReconnectInterval { get; }

        /// <summary>
        /// Gets the requested server/client heartbeat in seconds.
        /// </summary>
        ushort RequestedHeartbeat { get; }

        /// <summary>
        /// The connection state.
        /// </summary>
        AmqpConnectionStates State { get; }

        /// <summary>
        /// Whether or not the connection is currently open and ready for use.
        /// </summary>
        bool IsConnected { get; }

        #endregion Properties

        #region Events

        /// <summary>
        /// Occurs when the client connects to the broker.
        /// </summary>
        event EventHandler Connected;

        /// <summary>
        /// Occurs when the client disconnects from the broker.
        /// </summary>
        event EventHandler Disconnected;

        /// <summary>
        /// Occurs when the client is blocked from the broker.
        /// </summary>
        event EventHandler Blocked;

        /// <summary>
        /// Occurs each time the client attempts to reconnect to the broker.
        /// </summary>
        event EventHandler Reconnecting;

        /// <summary>
        /// Occurs when an exchange has been subscribed to.
        /// </summary>
        event AmqpExchangeSubscriptionEventHandler SubscribedToExchange;

        /// <summary>
        /// Occurs when an exchange has been unsubscribed from.
        /// </summary>
        event AmqpExchangeSubscriptionEventHandler UnsubscribedFromExchange;

        /// <summary>
        /// Occurs when an exchange has been subscribed to.
        /// </summary>
        event AmqpQueueSubscriptionEventHandler SubscribedToQueue;

        /// <summary>
        /// Occurs when an exchange has been unsubscribed from.
        /// </summary>
        event AmqpQueueSubscriptionEventHandler UnsubscribedFromQueue;

        #endregion Events

        #region Methods

        #region Connections

        /// <summary>
        /// Opens the broker connection.
        /// </summary>
        void Connect();

        /// <summary>
        /// Closes the broker connection.
        /// </summary>
        void Disconnect();

        #endregion Connections

        #region Subscriptions

        /// <summary>
        /// Adds an exchange subscription to the connection.
        /// </summary>
        /// <param name="subscription">The subscription to add.</param>        
        void Subscribe(AmqpExchangeSubscription subscription);

        /// <summary>
        /// Removes an existing exchange subscription from the connection.
        /// </summary>
        /// <param name="subscription">The subscription to remove.</param>
        void Unsubscribe(AmqpExchangeSubscription subscription);

        /// <summary>
        /// Adds a queue subscription to the connection.
        /// </summary>
        /// <param name="subscription">The subscription to add.</param>        
        void Subscribe(AmqpQueueSubscription subscription);

        /// <summary>
        /// Removes an existing queue subscription from the connection.
        /// </summary>
        /// <param name="subscription">The subscription to remove.</param>
        void Unsubscribe(AmqpQueueSubscription subscription);

        #endregion Subscriptions

        #region Publishing

        /// <summary>
        /// Publishes a message to the specified exchange with the specified optional routing key.
        /// </summary>
        /// <param name="exchange">The name of the exchange to publish to.</param>
        /// <param name="routingKey">The optional routing key to publish with.</param>
        /// <param name="body">The body to publish.</param>
        /// <param name="mandatory">Whether or not to publish with the AMQP "mandatory" flag.</param>
        /// <param name="immediate">Whether or not to publish with the AMQP "immediate" flag.</param>
        void Publish(string exchange, string routingKey, string body, bool mandatory = false, bool immediate = false);

        /// <summary>
        /// Publishes a message to the specified exchange with the specified optional routing key.
        /// </summary>
        /// <param name="exchange">The name of the exchange to publish to.</param>
        /// <param name="routingKey">The optional routing key to publish with.</param>
        /// <param name="body">The body to publish.</param>
        /// <param name="mandatory">Whether or not to publish with the AMQP "mandatory" flag.</param>
        /// <param name="immediate">Whether or not to publish with the AMQP "immediate" flag.</param>
        void Publish(string exchange, string routingKey, byte[] body, bool mandatory = false, bool immediate = false);

        /// <summary>
        /// Publishes a message to the specified exchange with the specified optional routing key.
        /// </summary>
        /// <param name="exchange">The name of the exchange to publish to.</param>
        /// <param name="routingKey">The optional routing key to publish with.</param>
        /// <param name="properties">The optional message properties to publish with (can be NULL).</param>
        /// <param name="body">The body to publish.</param>
        /// <param name="mandatory">Whether or not to publish with the AMQP "mandatory" flag.</param>
        /// <param name="immediate">Whether or not to publish with the AMQP "immediate" flag.</param>
        void Publish(string exchange, string routingKey, IAmqpMessageProperties properties, string body, bool mandatory = false, bool immediate = false);

        /// <summary>
        /// Publishes a message to the specified exchange with the specified optional routing key.
        /// </summary>
        /// <param name="exchange">The name of the exchange to publish to.</param>
        /// <param name="routingKey">The optional routing key to publish with.</param>
        /// <param name="properties">The optional message properties to publish with (can be NULL).</param>
        /// <param name="body">The body to publish.</param>
        /// <param name="mandatory">Whether or not to publish with the AMQP "mandatory" flag.</param>
        /// <param name="immediate">Whether or not to publish with the AMQP "immediate" flag.</param>
        void Publish(string exchange, string routingKey, IAmqpMessageProperties properties, byte[] body, bool mandatory = false, bool immediate = false);

        #endregion Publishing

        #region Exchanges

        /// <summary>
        /// Gets a list of exchanges for the current connection.
        /// </summary>
        /// <param name="virtualHost">The optional virtual host to get exchanges for. If NULL the connection's default virtual host is used.</param>
        /// <returns>A list of AMQP exchanges for the current connection.</returns>
        AmqpExchange[] GetExchanges(string virtualHost = null);

        #endregion Exchanges

        #region Queues

        /// <summary>
        /// Gets a list of queues for the current connection.
        /// </summary>
        /// <param name="virtualHost">The optional virtual host to get queues for. If NULL the connection's default virtual host is used.</param>
        /// <returns>A list of AMQP queues for the current connection.</returns>
        AmqpQueue[] GetQueues(string virtualHost = null);

        #endregion Queues

        #endregion Methods
    }
}

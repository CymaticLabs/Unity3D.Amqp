using System;
using System.Collections.Generic;
using System.Threading;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace CymaticLabs.Unity3D.Amqp.RabbitMq
{
    /// <summary>
    /// Represents a client connection to a RabbitMQ AMQP broker.
    /// </summary>
    public class RabbitMqBrokerConnection : IAmqpBrokerConnection
    {
        #region Fields

        // The connection state
        AmqpConnectionStates state = AmqpConnectionStates.Disconnected;

        // Used to synchronize the connection state
        object stateLock = new object();

        // List of current exchange subscriptions
        List<AmqpExchangeSubscription> exchangeSubscriptions;

        // List of current queue subscriptions
        List<AmqpQueueSubscription> queueSubscriptions;

        #endregion Fields

        #region Properties

        /// <summary>
        /// The name of the broker connection.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// The name or IP of the host broker.
        /// </summary>
        public string Server { get; private set; }

        /// <summary>
        /// The host broker's AMQP port number.
        /// </summary>
        public int Port { get; private set; }

        /// <summary>
        /// The broker vhost to use. Default is '/'.
        /// </summary>
        public string VirtualHost { get; private set; }

        /// <summary>
        /// The username for the client connection.
        /// </summary>
        public string Username { get; private set; }

        /// <summary>
        /// The password for the client connection.
        /// </summary>
        public string Password { get; private set; }

        /// <summary>
        /// Gets the number of seconds between reconnection attempts.
        /// </summary>
        public short ReconnectInterval { get; private set; }

        /// <summary>
        /// Gets the requested server/client heartbeat in seconds.
        /// </summary>
        public ushort RequestedHeartbeat { get; private set; }

        /// <summary>
        /// The client's connection.
        /// </summary>
        public IConnection Connection { get; private set; }

        /// <summary>
        /// The client's AMQP channel.
        /// </summary>
        public IModel Channel { get; private set; }

        /// <summary>
        /// The connection state.
        /// </summary>
        public AmqpConnectionStates State
        {
            get { return state; }

            private set
            {
                if (state != value)
                {
                    lock (stateLock)
                    {
                        state = value;
                    }
                }
            }
        }

        /// <summary>
        /// Whether or not the connection is currently open and ready for use.
        /// </summary>
        public bool IsConnected
        {
            get
            {
                EnsureConnection();
                return State == AmqpConnectionStates.Connected;
            }
        }

        #endregion Properties

        #region Events

        /// <summary>
        /// Occurs when the client connects to the broker.
        /// </summary>
        public event EventHandler Connected;

        /// <summary>
        /// Occurs when the client disconnects from the broker.
        /// </summary>
        public event EventHandler Disconnected;

        /// <summary>
        /// Occurs when the client is blocked from the broker.
        /// </summary>
        public event EventHandler Blocked;

        /// <summary>
        /// Occurs each time the client attempts to reconnect to the broker.
        /// </summary>
        public event EventHandler Reconnecting;

        /// <summary>
        /// Occurs when an exchange has been subscribed to.
        /// </summary>
        public event AmqpExchangeSubscriptionEventHandler SubscribedToExchange;

        /// <summary>
        /// Occurs when an exchange has been unsubscribed from.
        /// </summary>
        public event AmqpExchangeSubscriptionEventHandler UnsubscribedFromExchange;

        /// <summary>
        /// Occurs when an exchange has been subscribed to.
        /// </summary>
        public event AmqpQueueSubscriptionEventHandler SubscribedToQueue;

        /// <summary>
        /// Occurs when an exchange has been unsubscribed from.
        /// </summary>
        public event AmqpQueueSubscriptionEventHandler UnsubscribedFromQueue;

        #endregion Events

        #region Constructors

        /// <summary>
        /// Initializes a new broker connection with the given host, port, and credentials.
        /// </summary>
        /// <param name="server">The host server name or IP.</param>
        /// <param name="port">The host port number.</param>
        /// <param name="virtualHost">The broker virtual host to use.</param>
        /// <param name="username">The connection username.</param>
        /// <param name="password">The connection password.</param>
        /// <param name="reconnectInterval">The number of seconds to wait before connection retry attempts.</param>
        /// <param name="requestedHeartbeat">The client/server heartbeat in seconds.</param>
        public RabbitMqBrokerConnection(string server, int port, string virtualHost, string username, string password, short reconnectInterval = 5, ushort requestedHeartbeat = 30)
            : this("RabbitMQ Broker Connection", server, port, virtualHost, username, password, reconnectInterval, requestedHeartbeat)
        { }

        /// <summary>
        /// Initializes a new broker connection with the given host, port, and credentials.
        /// </summary>
        /// <param name="name">The name to give the connection.</param>
        /// <param name="server">The host server name or IP.</param>
        /// <param name="port">The host port number.</param>
        /// <param name="virtualHost">The broker virtual host to use.</param>
        /// <param name="username">The connection username.</param>
        /// <param name="password">The connection password.</param>
        /// <param name="reconnectInterval">The number of seconds to wait before connection retry attempts.</param>
        /// <param name="requestedHeartbeat">The client/server heartbeat in seconds.</param>
        public RabbitMqBrokerConnection(string name, string server, int port, string virtualHost, 
            string username, string password, short reconnectInterval = 5, ushort requestedHeartbeat = 30)
        {
            Name = name;
            Server = server;
            Port = port;
            VirtualHost = virtualHost;
            Username = username;
            Password = password;
            State = AmqpConnectionStates.Disconnected;
            ReconnectInterval = reconnectInterval;
            RequestedHeartbeat = requestedHeartbeat;
            exchangeSubscriptions = new List<AmqpExchangeSubscription>();
            queueSubscriptions = new List<AmqpQueueSubscription>();
        }

        #endregion Constructors

        #region Methods

        #region Connection

        // Ensures the client is in a connected state
        private void EnsureConnection()
        {
            // Ensure that the cliient is connected
            if (State == AmqpConnectionStates.Connected && (Connection == null || !Connection.IsOpen))
            {
                State = AmqpConnectionStates.Disconnected;
                if (Disconnected != null) Disconnected(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Opens the broker connection.
        /// </summary>
        public void Connect()
        {
            if (IsConnected) return;
            State = AmqpConnectionStates.Connecting;
            //StreamRuntime.Current.LogInfo("Connecting to {0}...", this);
            Console.WriteLine("Connecting to {0}...", this);

            // Begin the connection process
            int retryCount = 0;
            ThreadPool.QueueUserWorkItem(state =>
            {
                // Initialize the connection object
                IConnection connection = null;

                // Use a threaded while loop approach to keep reattempting connection
                while (State == AmqpConnectionStates.Connecting && (connection == null || !connection.IsOpen))
                {
                    try
                    {
                        // Get the current instance as the state
                        var bc = (RabbitMqBrokerConnection)state;

                        var factory = new ConnectionFactory()
                        {
                            AutomaticRecoveryEnabled = false,
                            HostName = Server,
                            Port = Port,
                            VirtualHost = VirtualHost,
                            UserName = Username,
                            Password = Password,
                            RequestedHeartbeat = RequestedHeartbeat
                        };

                        // Enable encrypted connection based on port
                        factory.Ssl.Enabled = Port == 5671 ? true : false;

                        // TODO Remove this line and setup proper certification registration policy for tighter security
                        factory.Ssl.AcceptablePolicyErrors = System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch;

                        // Create the connection for the connection
                        connection = factory.CreateConnection();
                        var channel = connection.CreateModel();

                        // Assign the AMQP client connection and channel
                        bc.Connection = connection;
                        bc.Channel = channel;

                        // Listen for connection state change events
                        connection.ConnectionBlocked += Connection_ConnectionBlocked;
                        connection.ConnectionShutdown += Connection_ConnectionShutdown;
                        connection.ConnectionUnblocked += Connection_ConnectionUnblocked;

                        // Reset retries
                        retryCount = 0;

                        bc.State = AmqpConnectionStates.Connected;
                        //StreamRuntime.Current.LogInfo("Connected to {0}", this);
                        Console.WriteLine("Connected to {0}", this);
                        if (Connected != null) Connected(this, EventArgs.Empty);
                    }
                    catch (Exception ex)
                    {
                        // Authentication exceptions ex.Message property is not very useful, so catch for it
                        if (ex.InnerException is AuthenticationFailureException)
                        {
                            var errorMessage = "Authentication Failed";
                            //StreamRuntime.Current.LogError("Error connecting to {0} => {1}", this, errorMessage);
                            Console.WriteLine("Error connecting to {0} => {1}", this, errorMessage);

                            // Get the current instance as the state
                            var bc = (RabbitMqBrokerConnection)state;

                            // Don't attempt to reconnect, we know the credentials are bad
                            bc.State = AmqpConnectionStates.Disconnected;
                            if (Disconnected != null) Disconnected(this, EventArgs.Empty);
                        }
                        else
                        {
                            // Report retries, but double the count at which we report to avoid flooding logs with many retry attempts
                            if (++retryCount == 1 || retryCount % 2 == 0)
                            {
                                //StreamRuntime.Current.LogError("(retries:{0}) Error connecting to {1} => {2}",
                                //    retryCount, this, ex.Message);
                                Console.WriteLine("(retries:{0}) Error connecting to {1} => {2}", retryCount, this, ex.Message);
                                if (Reconnecting != null) Reconnecting(this, EventArgs.Empty);
                            }

                            // Sleep a bit before retrying to connect
                            Thread.Sleep(ReconnectInterval * 1000);
                        }
                    }
                }
            }, this);
        }

        /// <summary>
        /// Closes the broker connection.
        /// </summary>
        public void Disconnect()
        {
            lock(this)
            {
                if (!IsConnected) return;

                State = AmqpConnectionStates.Disconnecting;
                Console.WriteLine("Disconnecting from {0}...", this);
                exchangeSubscriptions.Clear();
                Connection.Close();
                State = AmqpConnectionStates.Disconnected;
                Console.WriteLine("Disconnected from {0}", this);
                if (Disconnected != null) Disconnected(this, EventArgs.Empty);
            }
        }

        #region Event Handlers

        // Handles the shutdown of a connection
        private void Connection_ConnectionShutdown(object sender, ShutdownEventArgs e)
        {
            State = AmqpConnectionStates.Disconnected;
            if (Disconnected != null) Disconnected(this, EventArgs.Empty);
        }

        // Handles the blocking of a connection.
        private void Connection_ConnectionBlocked(object sender, RabbitMQ.Client.Events.ConnectionBlockedEventArgs e)
        {
            State = AmqpConnectionStates.Blocked;
            if (Blocked != null) Blocked(this, EventArgs.Empty);
        }

        // Handles the unblocking of a connection
        private void Connection_ConnectionUnblocked(IConnection sender)
        {
            if (State == AmqpConnectionStates.Blocked) State = AmqpConnectionStates.Disconnected;
            if (Disconnected != null) Disconnected(this, EventArgs.Empty);
        }

        #endregion Event Handlers

        #endregion

        #region Subscriptions

        #region Exchange

        /// <summary>
        /// Adds an exchange subscription to the connection.
        /// </summary>
        /// <param name="subscription">The subscription to add.</param>        
        public void Subscribe(AmqpExchangeSubscription subscription)
        {
            if (subscription == null) throw new ArgumentNullException("subscription");
            if (exchangeSubscriptions.Contains(subscription)) throw new Exception("Subscription already exists.");
            if (!IsConnected) throw new InvalidOperationException("Must be connected before subscribing.");

            // The connection for the subscription is currently blocked so we can't update anything
            if (State == AmqpConnectionStates.Blocked) return;

            // Ensure a subscription handler has been assigned/is available
            if (subscription.Handler == null) throw new ArgumentNullException("subscription.Handler cannot be null");

            // Ensure an exchange name has been defined
            if (string.IsNullOrEmpty(subscription.ExchangeName)) throw new ArgumentNullException("subscription.ExchangeName cannot be null");

            lock (Channel)
            {
                // Ensure the exchange and queue
                var exchangeName = subscription.ExchangeName;

                // First declare the exchange
                var exchangeType = subscription.ExchangeType.ToString().ToLower();

                // Attempt to verify the exchange type, passing the wrong exchange breaks the client connection
                var typeStr = exchangeType;

                try
                {
                    Channel.ExchangeDeclare(exchangeName, typeStr, true, false, null);
                }
                catch (Exception e1)
                {
                    // If there was a problem declaring the exchange, for now close the broker connection and reset the connection state
                    Console.WriteLine("Resetting connection for broker '{0}' - broker error occured:\n\n{1}", this, e1.Message);
                    Disconnect();
                    Connect();

                    // The whole broker connection was just reset, so let it restore the state and abort current actions
                    return;
                }

                // Next create a queue and bind it to the exchange
                var queueName = Channel.QueueDeclare().QueueName;
                var routingKey = subscription.ExchangeType != AmqpExchangeTypes.Fanout ? subscription.RoutingKey : "";
                Channel.QueueBind(queueName, exchangeName, routingKey);

                // Create an event-based message consumer
                var consumer = new EventingBasicConsumer(Channel);

                // Connect handler
                consumer.Received += (o, e) =>
                {
                    if (subscription.Handler == null && subscription.ThreadsafeHandler == null)
                    {
                        Console.WriteLine("Error: missing subscription handler: {0}", subscription);
                    }
                    else if (subscription.Enabled)
                    {
                        var handler = subscription.ThreadsafeHandler != null ? subscription.ThreadsafeHandler : subscription.Handler;
                        handler(new AmqpExchangeReceivedMessage(subscription, new RabbitMqReceivedMessage(e)));
                    }
                };

                // Subscribe/consume and store the consumer tag so we can cancel the consumer later if the subscription changes
                subscription.ConsumerTag = Channel.BasicConsume(queueName, true, consumer);
                subscription.Consumer = consumer;

                // Store the connection
                subscription.Connection = this;

                Console.WriteLine("Subscribed to {0}{1} on {2}", subscription.ExchangeName, subscription.RoutingKey, subscription.Connection);
            }
            
            exchangeSubscriptions.Add(subscription);

            // Notify
            if (SubscribedToExchange != null) SubscribedToExchange(subscription);
        }

        /// <summary>
        /// Removes an existing exchange subscription from the connection.
        /// </summary>
        /// <param name="subscription">The subscription to add.</param>
        public void Unsubscribe(AmqpExchangeSubscription subscription)
        {
            if (subscription == null) throw new ArgumentNullException("subscription");
            if (!exchangeSubscriptions.Contains(subscription)) throw new Exception("Subscription does not exist.");
            if (!IsConnected) throw new InvalidOperationException("Must be connected before unsubscribing.");

            // Cancel the consumer
            try
            {
                lock (Channel)
                {
                    Channel.BasicCancel(subscription.ConsumerTag);
                    var logRoutingKey = !string.IsNullOrEmpty(subscription.RoutingKey) ? " => " + subscription.RoutingKey : "";
                    //log.InfoFormat("Unsubscribed from {0}{1} on {2}", sub.Exchange, logRoutingKey, client.Name);
                    Console.WriteLine("Unsubscribed from {0}{1} on {2}", subscription.ExchangeName, logRoutingKey, this);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error ubsubscribing: {0}", ex);
            }

            exchangeSubscriptions.Remove(subscription);

            // Notify
            if (UnsubscribedFromExchange != null) UnsubscribedFromExchange(subscription);
        }

        #endregion Exchange

        #region Queue

        /// <summary>
        /// Adds a queue subscription to the connection.
        /// </summary>
        /// <param name="subscription">The subscription to add.</param>        
        public void Subscribe(AmqpQueueSubscription subscription)
        {
            if (subscription == null) throw new ArgumentNullException("subscription");
            if (queueSubscriptions.Contains(subscription)) throw new Exception("Subscription already exists.");
            if (!IsConnected) throw new InvalidOperationException("Must be connected before subscribing.");

            // The connection for the subscription is currently blocked so we can't update anything
            if (State == AmqpConnectionStates.Blocked) return;

            // Ensure a subscription handler has been assigned/is available
            if (subscription.Handler == null) throw new ArgumentNullException("subscription.Handler cannot be null");

            // Ensure an exchange name has been defined
            if (string.IsNullOrEmpty(subscription.QueueName)) throw new ArgumentNullException("subscription.QueueName cannot be null");

            try
            {
                lock(Channel)
                {
                    // Create a queue/message consumer
                    var consumer = new EventingBasicConsumer(Channel);
                    subscription.Consumer = consumer;

                    // Apply the handler
                    consumer.Received += (o, e) =>
                    {
                        if (subscription.Handler == null && subscription.ThreadsafeHandler == null)
                        {
                            Console.WriteLine("Error: missing subscription handler: {0}", subscription);
                        }
                        else if (subscription.Enabled)
                        {
                            var handler = subscription.ThreadsafeHandler != null ? subscription.ThreadsafeHandler : subscription.Handler;
                            handler(new AmqpQueueReceivedMessage(subscription, new RabbitMqReceivedMessage(e)));
                        }
                    };

                    // Begin consuming messages
                    Channel.BasicConsume(subscription.QueueName, !subscription.UseAck, consumer);
                    Console.WriteLine("Subscribed to queue {0} on {1}", subscription.QueueName, this);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error subscribing to queue {0} on {1}\n\n{2}", subscription.QueueName, this, ex);
            }

            // Add to the list
            queueSubscriptions.Add(subscription);

            // Notify
            if (SubscribedToQueue != null) SubscribedToQueue(subscription);
        }

        /// <summary>
        /// Removes an existing queue subscription from the connection.
        /// </summary>
        /// <param name="subscription">The subscription to add.</param>
        public void Unsubscribe(AmqpQueueSubscription subscription)
        {
            if (subscription == null) throw new ArgumentNullException("subscription");
            if (!queueSubscriptions.Contains(subscription)) throw new Exception("Subscription does not exist.");
            if (!IsConnected) throw new InvalidOperationException("Must be connected before unsubscribing.");

            // Cancel the consumer
            try
            {
                lock(Channel)
                {
                    Channel.BasicCancel(subscription.ConsumerTag);
                    Console.WriteLine("{0} has unsubscribed from queue: {1}", this, subscription.QueueName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error on {0} unsubscribing from queue: {1}\n\n{2}", this, subscription.QueueName, ex);
            }

            // Remove from list
            queueSubscriptions.Remove(subscription);

            // Notify
            if (UnsubscribedFromQueue != null) UnsubscribedFromQueue(subscription);
        }

        #endregion Queue

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
        public void Publish(string exchange, string routingKey, string body, bool mandatory = false, bool immediate = false)
        {
            if (string.IsNullOrEmpty(body)) throw new ArgumentNullException("body");

            // Convert the body to bytes
            var bytes = System.Text.Encoding.UTF8.GetBytes(body);
            Publish(exchange, routingKey, null, bytes, mandatory, immediate);
        }

        /// <summary>
        /// Publishes a message to the specified exchange with the specified optional routing key.
        /// </summary>
        /// <param name="exchange">The name of the exchange to publish to.</param>
        /// <param name="routingKey">The optional routing key to publish with.</param>
        /// <param name="body">The body to publish.</param>
        /// <param name="mandatory">Whether or not to publish with the AMQP "mandatory" flag.</param>
        /// <param name="immediate">Whether or not to publish with the AMQP "immediate" flag.</param>
        public void Publish(string exchange, string routingKey, byte[] body, bool mandatory = false, bool immediate = false)
        {
            Publish(exchange, routingKey, body, mandatory, immediate);
        }

        /// <summary>
        /// Publishes a message to the specified exchange with the specified optional routing key.
        /// </summary>
        /// <param name="exchange">The name of the exchange to publish to.</param>
        /// <param name="routingKey">The optional routing key to publish with.</param>
        /// <param name="properties">The optional message properties to publish with (can be NULL).</param>
        /// <param name="body">The body to publish.</param>
        /// <param name="mandatory">Whether or not to publish with the AMQP "mandatory" flag.</param>
        /// <param name="immediate">Whether or not to publish with the AMQP "immediate" flag.</param>
        public void Publish(string exchange, string routingKey, IAmqpMessageProperties properties, string body, bool mandatory = false, bool immediate = false)
        {
            if (string.IsNullOrEmpty(body)) throw new ArgumentNullException("body");

            // Convert the body to bytes
            var bytes = System.Text.Encoding.UTF8.GetBytes(body);
            Publish(exchange, routingKey, properties, bytes, mandatory, immediate);
        }

        /// <summary>
        /// Publishes a message to the specified exchange with the specified optional routing key.
        /// </summary>
        /// <param name="exchange">The name of the exchange to publish to.</param>
        /// <param name="routingKey">The optional routing key to publish with.</param>
        /// <param name="properties">The optional message properties to publish with (can be NULL).</param>
        /// <param name="body">The body to publish.</param>
        /// <param name="mandatory">Whether or not to publish with the AMQP "mandatory" flag.</param>
        /// <param name="immediate">Whether or not to publish with the AMQP "immediate" flag.</param>
        public void Publish(string exchange, string routingKey, IAmqpMessageProperties properties, byte[] body, bool mandatory = false, bool immediate = false)
        {
            if (string.IsNullOrEmpty(exchange)) throw new ArgumentNullException("exchange");
            if (routingKey == null) routingKey = "";
            if (body == null) throw new ArgumentNullException("body");
            if (body.Length == 0) throw new ArgumentException("body length cannot be 0");

            // Initialize NULL message properties
            IBasicProperties props = null;

            // If properties were supplied
            if (properties != null)
            {
                // Attempt to gave the native properties
                var rabbitProps = properties as RabbitMqMessageProperties;
                props = rabbitProps != null ? rabbitProps.GetWrapped() : null;

                // If properties are still NULL, create a new RabbitMQ properties object
                props = Channel.CreateBasicProperties();
                props.AppId = properties.AppId;
                props.ClusterId = properties.ClusterId;
                props.ContentEncoding = properties.ContentEncoding;
                props.ContentType = properties.ContentType;
                props.CorrelationId = properties.CorrelationId;
                props.DeliveryMode = properties.DeliveryMode;
                props.Expiration = properties.Expiration;
                props.Headers = properties.Headers;
                props.MessageId = properties.MessageId;
                props.Priority = properties.Priority;
                props.ReplyTo = properties.ReplyTo;
                props.ReplyToAddress = PublicationAddress.Parse(properties.ReplyToAddress);
                props.Timestamp = new AmqpTimestamp(properties.Timestamp);
                props.Type = properties.Type;
                props.UserId = properties.UserId;
            }

            Channel.BasicPublish(exchange, routingKey, mandatory, immediate, props, body);
        }

        #endregion Publishing

        public override string ToString()
        {
            return AmqpHelper.GetConnectionInfo(this);
        }

        #endregion Methods
    }
}

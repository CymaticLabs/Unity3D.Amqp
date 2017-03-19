using System;
using System.Collections.Generic;
using System.Threading;
using System.Net;
using System.IO;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using CymaticLabs.Unity3D.Amqp.SimpleJSON;

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

        // Whether or not the client will attempt to reconnect upon disconnection
        bool reconnect = false;

        // The number of reconnection retries
        int connectionRetryCount;

        // An internal list of the number of reconnect retries when errors occur during subscriptions (which cause the whole client to disconnect)
        // This is implemented to avoid an infinite connect->subscribe->error->drop/disconnect->reconnect->infinity loop
        int subscribeRetryCount = 0;

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
        public int AmqpPort { get; private set; }

        /// <summary>
        /// The host broker's web/REST API port number.
        /// </summary>
        public int WebPort { get; private set; }

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
        /// Gets or sets the maximum number of times the client will attempt to reconnect to the host before aborting.
        /// </summary>
        public uint ReconnectRetryLimit { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of failed subscriptions the client will tolerate before preventing connection to the host.
        /// </summary>
        /// <remarks>
        /// Presently in RabbitMQ if there is an error during subscription the host will close the connection
        /// so the client must reconnect. In cases where the client attempts to resubscribe the same failing subscription
        /// this can lead to an endless loop of connect->subscribe->error->reconnect->infinity. Putting a limit
        /// prevents the loop from going on infinitely.
        /// </remarks>
        public byte SubscribeRetryLimit { get; set; }

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
        /// Occurs when the client aborts attempting to automatically reconnect because it reached
        /// one of its retry attempt limits. When a connection is aborted <see cref="ResetConnection"/>
        /// must be called before attemtping any further connections. This feature is implemented to 
        /// prevent infinite connect->error->disconnect->reconnect->infinity scenarios.
        /// </summary>
        public event EventHandler ConnectionAborted;

        /// <summary>
        /// Occurs when there is a connection error.
        /// </summary>
        public event EventHandler<ExceptionEventArgs> ConnectionError;

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

        /// <summary>
        /// Occurs when there is an error subscribing to an exchange.
        /// </summary>
        public event EventHandler<ExceptionEventArgs> ExchangeSubscribeError;

        /// <summary>
        /// Occurs when there is an error subscribing to a queue.
        /// </summary>
        public event EventHandler<ExceptionEventArgs> QueueSubscribeError;

        /// <summary>
        /// Occurs when there is an error unsubscribing from an exchange.
        /// </summary>
        public event EventHandler<ExceptionEventArgs> ExchangeUnsubscribeError;

        /// <summary>
        /// Occurs when there is an error unsubscribing from a queue.
        /// </summary>
        public event EventHandler<ExceptionEventArgs> QueueUnsubscribeError;

        #endregion Events

        #region Constructors

        /// <summary>
        /// Initializes a new broker connection with the given host, port, and credentials.
        /// </summary>
        /// <param name="server">The host server name or IP.</param>
        /// <param name="amqpPort">The host AMQP port number.</param>
        /// <param name="webPort">The host AMQP web/REST port number.</param>
        /// <param name="virtualHost">The broker virtual host to use.</param>
        /// <param name="username">The connection username.</param>
        /// <param name="password">The connection password.</param>
        /// <param name="reconnectInterval">The number of seconds to wait before connection retry attempts.</param>
        /// <param name="requestedHeartbeat">The client/server heartbeat in seconds.</param>
        public RabbitMqBrokerConnection(string server, int amqpPort, int webPort, string virtualHost, string username, string password, short reconnectInterval = 5, ushort requestedHeartbeat = 30)
            : this("RabbitMQ Broker Connection", server, amqpPort, webPort, virtualHost, username, password, reconnectInterval, requestedHeartbeat)
        { }

        /// <summary>
        /// Initializes a new broker connection with the given host, port, and credentials.
        /// </summary>
        /// <param name="name">The name to give the connection.</param>
        /// <param name="server">The host server name or IP.</param>
        /// <param name="amqpPort">The host AMQP port number.</param>
        /// <param name="webPort">The host AMQP web/REST port number.</param>
        /// <param name="virtualHost">The broker virtual host to use.</param>
        /// <param name="username">The connection username.</param>
        /// <param name="password">The connection password.</param>
        /// <param name="reconnectInterval">The number of seconds to wait before connection retry attempts.</param>
        /// <param name="requestedHeartbeat">The client/server heartbeat in seconds.</param>
        public RabbitMqBrokerConnection(string name, string server, int amqpPort, int webPort, string virtualHost, 
            string username, string password, short reconnectInterval = 5, ushort requestedHeartbeat = 30)
        {
            Name = name;
            Server = server;
            AmqpPort = amqpPort;
            WebPort = webPort;
            VirtualHost = virtualHost;
            Username = username;
            Password = password;
            State = AmqpConnectionStates.Disconnected;
            ReconnectInterval = reconnectInterval;
            RequestedHeartbeat = requestedHeartbeat;
            exchangeSubscriptions = new List<AmqpExchangeSubscription>();
            queueSubscriptions = new List<AmqpQueueSubscription>();

            ReconnectRetryLimit = int.MaxValue;
            SubscribeRetryLimit = 10;
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
                lock(stateLock) State = AmqpConnectionStates.Disconnected;
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Opens the broker connection.
        /// </summary>
        public void Connect()
        {
            // Ensure 
            if (connectionRetryCount >= ReconnectRetryLimit || State == AmqpConnectionStates.Aborted)
            {
                Console.WriteLine("Connection is currently in an aborted state and must be reset before further connection attempts.");
                if (Connection != null && Connection.IsOpen) Connection.Close();
                State = AmqpConnectionStates.Aborted;
                ConnectionAborted?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (IsConnected) return;
            lock(stateLock) State = AmqpConnectionStates.Connecting;

            //StreamRuntime.Current.LogInfo("Connecting to {0}...", this);
            Console.WriteLine("Connecting to {0}...", this);

            // Begin the connection process
            ThreadPool.QueueUserWorkItem(state =>
            {
                // Initialize the connection object
                IConnection connection = null;

                // Use a threaded while loop approach to keep reattempting connection
                while (State == AmqpConnectionStates.Connecting && connectionRetryCount < ReconnectRetryLimit && (connection == null || !connection.IsOpen))
                {
                    try
                    {
                        // Get the current instance as the state
                        var bc = (RabbitMqBrokerConnection)state;

                        var factory = new ConnectionFactory()
                        {
                            AutomaticRecoveryEnabled = false,
                            HostName = Server,
                            Port = AmqpPort,
                            VirtualHost = VirtualHost,
                            UserName = Username,
                            Password = Password,
                            RequestedHeartbeat = RequestedHeartbeat
                        };

                        // Enable encrypted connection based on port
                        factory.Ssl.Enabled = AmqpPort == 5671 ? true : false;
                        factory.Ssl.ServerName = Server; // this is needed so that Mono won't have an exception when it's NULL

                        // If relaxed SSL validation is set, apply relaxations (this helps with many Mono SSL certificate verification issues, but is less secure)
                        if (SslHelper.RelaxedValidation)
                        {
                            // Add in custom SSL certificate validator so we have the ability to enable untrusted SSL certificates and deal with Mono issues
                            factory.Ssl.CertificateValidationCallback = SslHelper.RemoteCertificateValidationCallback;

                            // TODO Eventually expand the granularity of configurable policies
                            factory.Ssl.AcceptablePolicyErrors = System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch;
                        }

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
                        lock(stateLock) connectionRetryCount = 0;
                        reconnect = false;

                        bc.State = AmqpConnectionStates.Connected;
                        //StreamRuntime.Current.LogInfo("Connected to {0}", this);
                        Console.WriteLine("Connected to {0}", this);
                        Connected?.Invoke(this, EventArgs.Empty);
                        return;
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
                            Disconnected?.Invoke(this, EventArgs.Empty);
                            ConnectionError?.Invoke(this, new ExceptionEventArgs(ex));
                            return;
                        }
                        else
                        {
                            // Update retry attempt  
                            lock(stateLock) connectionRetryCount++;
                            Console.WriteLine("(retries:{0}) Error connecting to {1} => {2}", connectionRetryCount, this, ex.Message);
                            ConnectionError?.Invoke(this, new ExceptionEventArgs(ex));

                            // If the connection retry limit was reached...
                            if (connectionRetryCount >= ReconnectRetryLimit)
                            {
                                // Notify
                                Disconnect();
                                State = AmqpConnectionStates.Aborted;
                                ConnectionAborted?.Invoke(this, EventArgs.Empty);
                                Console.WriteLine("AMQP Reconnection limit reached; connection aborted.");
                                return;
                            }
                            // Otherwise sleep a bit before retrying to connect
                            else
                            {
                                Reconnecting?.Invoke(this, EventArgs.Empty);
                                Thread.Sleep(ReconnectInterval * 1000);
                            }
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
            if (!IsConnected) return;

            lock (stateLock)
            {
                State = AmqpConnectionStates.Disconnecting;
            }

            Console.WriteLine("Disconnecting from {0}...", this);

            exchangeSubscriptions.Clear();
            Connection.Close();

            lock(stateLock)
            { 
                State = AmqpConnectionStates.Disconnected;
            }

            Console.WriteLine("Disconnected from {0}", this);
            Disconnected?.Invoke(this, EventArgs.Empty);

            // If a reconnection is pending, carry it out
            if (reconnect)
            {
                reconnect = false;
                Thread.Sleep(1000); // wait a minimum amount of time before attempting to reconnect
                Connect();
            }
        }

        /// <summary>
        /// Resets the connection and clears things like reconnect counters to allow 
        /// reconnection attempts after the retry limits have been reached which normally prevent any further reconnection.
        /// </summary>
        public void ResetConnection()
        {
            lock(stateLock)
            {
                // Reset state
                State = AmqpConnectionStates.Disconnected;

                // Reset retry counters
                connectionRetryCount = 0;
                subscribeRetryCount = 0;
            }
        }

        #region Event Handlers

        // Handles the shutdown of a connection
        private void Connection_ConnectionShutdown(object sender, ShutdownEventArgs e)
        {
            Console.WriteLine("AMQP connection closed by host");
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
        /// <returns>The exception (if any) that occurred during the operation.</returns>
        public Exception Subscribe(AmqpExchangeSubscription subscription)
        {
            if (subscription == null) throw new ArgumentNullException("subscription");
            if (exchangeSubscriptions.Contains(subscription)) throw new Exception("Subscription already exists.");
            if (!IsConnected) return new InvalidOperationException("Must be connected before subscribing.");

            // Subscription retry limit reached?
            if (subscribeRetryCount >= SubscribeRetryLimit || State == AmqpConnectionStates.Aborted)
            {
                if (Connection != null && Connection.IsOpen) Connection.Close();
                Console.WriteLine("Connection is currently in an aborted state and cannot process any more subscriptions");
                State = AmqpConnectionStates.Aborted;
                ConnectionAborted?.Invoke(this, EventArgs.Empty);
                return new Exception("Connection aborted");
            }

            // The connection for the subscription is currently blocked so we can't update anything
            if (State == AmqpConnectionStates.Blocked) return new Exception("AMQP broker connection is blocked");

            // Ensure a subscription handler has been assigned/is available
            if (subscription.Handler == null) throw new ArgumentNullException("subscription.Handler cannot be null");

            // Ensure an exchange name has been defined
            if (string.IsNullOrEmpty(subscription.ExchangeName)) throw new ArgumentNullException("subscription.ExchangeName cannot be null");

            //lock (Channel)
            //{
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
            catch (Exception ex)
            {
                // If the subscribe retry/reconnect limit hasn't been reach, attempt to restore the connection that was lost
                if (subscribeRetryCount++ < SubscribeRetryLimit)
                {
                    // If there was a problem declaring the exchange, for now close the broker connection and reset the connection state
                    Console.WriteLine("(retries:{0}) Resetting connection for broker '{1}' - broker error occured:\n{2}\n", subscribeRetryCount, this, ex.Message);
                    reconnect = true;
                    Disconnect();
                }
                else
                {
                    // Abort further reconnection attempts until connection reset
                    State = AmqpConnectionStates.Aborted;
                    ConnectionAborted?.Invoke(this, EventArgs.Empty);
                    Console.WriteLine("Subscription retry limit reached.");
                }

                // Notify
                ExchangeSubscribeError?.Invoke(this, new ExceptionEventArgs(ex));

                // The whole broker connection was just reset, so let it restore the state and abort current actions
                return ex;
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
            //}
            
            exchangeSubscriptions.Add(subscription);

            // Notify
            SubscribedToExchange?.Invoke(subscription);

            return null;
        }

        /// <summary>
        /// Removes an existing exchange subscription from the connection.
        /// </summary>
        /// <param name="subscription">The subscription to add.</param>
        /// <returns>The exception (if any) that occurred during the operation.</returns>
        public Exception Unsubscribe(AmqpExchangeSubscription subscription)
        {
            if (subscription == null) throw new ArgumentNullException("subscription");
            if (!exchangeSubscriptions.Contains(subscription)) throw new Exception("Subscription does not exist.");
            if (!IsConnected) return new InvalidOperationException("Must be connected before unsubscribing.");

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
                // Notify
                ExchangeUnsubscribeError?.Invoke(this, new ExceptionEventArgs(ex));
                var logRoutingKey = !string.IsNullOrEmpty(subscription.RoutingKey) ? " => " + subscription.RoutingKey : "";
                Console.WriteLine("Error ubsubscribing from {0}{1} {2}", subscription.ExchangeName, logRoutingKey, ex);
                return ex;
            }

            exchangeSubscriptions.Remove(subscription);

            // Notify
            UnsubscribedFromExchange?.Invoke(subscription);

            return null;
        }

        #endregion Exchange

        #region Queue

        /// <summary>
        /// Adds a queue subscription to the connection.
        /// </summary>
        /// <param name="subscription">The subscription to add.</param>
        /// <returns>The exception (if any) that occurred during the operation.</returns>
        public Exception Subscribe(AmqpQueueSubscription subscription)
        {
            if (subscription == null) throw new ArgumentNullException("subscription");
            if (queueSubscriptions.Contains(subscription)) throw new Exception("Subscription already exists.");
            if (!IsConnected) return new InvalidOperationException("Must be connected before subscribing.");

            // Subscription retry limit reached?
            if (subscribeRetryCount >= SubscribeRetryLimit || State == AmqpConnectionStates.Aborted)
            {
                if (Connection != null && Connection.IsOpen) Connection.Close();
                State = AmqpConnectionStates.Aborted;
                ConnectionAborted?.Invoke(this, EventArgs.Empty);
                Console.WriteLine("Connection is currently in an aborted state and cannot process any more subscriptions");
                return new Exception("Connection aborted");
            }

            // The connection for the subscription is currently blocked so we can't update anything
            if (State == AmqpConnectionStates.Blocked) return new Exception("AMQP connection blocked by host");

            // Ensure a subscription handler has been assigned/is available
            if (subscription.Handler == null) throw new ArgumentNullException("subscription.Handler cannot be null");

            // Ensure an exchange name has been defined
            if (string.IsNullOrEmpty(subscription.QueueName)) throw new ArgumentNullException("subscription.QueueName cannot be null");

            try
            {
                //lock(Channel)
                //{
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
                Console.WriteLine("Subscribed to {0} on {1}", subscription.QueueName, subscription.Connection);
                //}
            }
            catch (Exception ex)
            {
                // If the subscribe retry/reconnect limit hasn't been reach, attempt to restore the connection that was lost
                if (subscribeRetryCount++ < SubscribeRetryLimit)
                {
                    // If there was a problem declaring the exchange, for now close the broker connection and reset the connection state
                    Console.WriteLine("(retries:{0}) Resetting connection for broker '{1}' - broker error occured:\n{2}\n", subscribeRetryCount, this, ex.Message);
                    reconnect = true;
                    Disconnect();
                }
                else
                {
                    // Abort further reconnection attempts until connection reset
                    State = AmqpConnectionStates.Aborted;
                    ConnectionAborted?.Invoke(this, EventArgs.Empty);
                    Console.WriteLine("Subscription retry limit reached.");
                }

                // Notify
                QueueSubscribeError?.Invoke(this, new ExceptionEventArgs(ex));

                // The whole broker connection was just reset, so let it restore the state and abort current actions
                return ex;
            }

            // Add to the list
            queueSubscriptions.Add(subscription);

            // Notify
            SubscribedToQueue?.Invoke(subscription);

            return null;
        }

        /// <summary>
        /// Removes an existing queue subscription from the connection.
        /// </summary>
        /// <param name="subscription">The subscription to add.</param>
        public Exception Unsubscribe(AmqpQueueSubscription subscription)
        {
            if (subscription == null) throw new ArgumentNullException("subscription");
            if (!queueSubscriptions.Contains(subscription)) throw new Exception("Subscription does not exist.");
            if (!IsConnected) return new InvalidOperationException("Must be connected before unsubscribing.");

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
                // Notify
                QueueUnsubscribeError?.Invoke(this, new ExceptionEventArgs(ex));
                Console.WriteLine("Error on {0} unsubscribing from queue: {1}\n{2}\n", this, subscription.QueueName, ex);
                return ex;
            }

            // Remove from list
            queueSubscriptions.Remove(subscription);

            // Notify
            UnsubscribedFromQueue?.Invoke(subscription);

            return null;
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

        #region Exchanges

        /// <summary>
        /// Declares an exchange on the broker for the current virtual host.
        /// </summary>
        /// <param name="name">The name of the exchange to declare.</param>
        /// <param name="type">The type of exchange to declare.</param>
        /// <param name="durable">Whether or not the exchange should be durable.</param>
        /// <param name="autoDelete">Whether or not the exchange will have auto-delete enabled.</param>
        /// <param name="args">Optional exchange arguments.</param>
        /// <returns>An Exception if one occurred during the operation, otherwise NULL.</returns>
        public Exception DeclareExchange(string name, AmqpExchangeTypes type, bool durable = true, bool autoDelete = false, IDictionary<string, object> args = null)
        {
            if (IsConnected) throw new InvalidOperationException("Exchanges cannot be declared when disconnected");
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");

            try
            {
                lock (Channel)
                {
                    Channel.ExchangeDeclare(name, type.ToString().ToLower(), durable, autoDelete, args);
                }
            }
            catch (Exception ex)
            {
                return ex;
            }

            return null;
        }

        /// <summary>
        /// Declares an exchange on the broker for the current virtual host.
        /// </summary>
        /// <param name="name">The name of the exchange to delete</param>
        /// <param name="ifUnused">Only delete the exchange if it is currently unused.</param>
        /// <returns>An Exception if one occurred during the operation, otherwise NULL.</returns>
        public Exception DeleteExchange(string name, bool ifUnused = false)
        {
            if (IsConnected) throw new InvalidOperationException("Exchanges cannot be declared when disconnected");
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");

            try
            {
                lock (Channel)
                {
                    Channel.ExchangeDelete(name, ifUnused);
                }
            }
            catch (Exception ex)
            {
                return ex;
            }

            return null;
        }

        /// <summary>
        /// Gets whether or not an exchange by a given name exists.
        /// </summary>
        /// <param name="name">The name of the exchange to check for.</param>
        /// <param name="virtualHost">The optional virtual host to get exchanges for. If NULL the connection's default virtual host is used.</param>
        /// <returns>True if the exchange exists, False if not.</returns>
        public bool ExchangeExists(string name, string virtualHost = null)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");

            foreach (var exchange in GetExchanges(virtualHost))
            {
                if (exchange.Name == name) return true;
            }

            return false;
        }

        /// <summary>
        /// Gets a list of exchanges for the current connection.
        /// </summary>
        /// <param name="virtualHost">The optional virtual host to get exchanges for. If NULL the connection's default virtual host is used.</param>
        /// <returns>A list of AMQP exchanges for the current connection.</returns>
        public AmqpExchange[] GetExchanges(string virtualHost = null)
        {
            if (string.IsNullOrEmpty(Server)) throw new InvalidOperationException("Server cannot be NULL when making a request to its REST API");

            // If not supplied, use the default virtual host
            if (virtualHost == null) virtualHost = VirtualHost;

            // Create the web request for this broker
            var url = (WebPort == 443 ? "https" : "http") + "://" + Server + ":" + WebPort.ToString() + "/api/exchanges/" + virtualHost;
            var request = (HttpWebRequest)WebRequest.Create(url);

            // Add authorization info
            string authInfo = Username + ":" + Password;
            authInfo = Convert.ToBase64String(System.Text.Encoding.Default.GetBytes(authInfo));
            request.Headers["Authorization"] = "Basic " + authInfo;

            // Apply the request and retrieve the response stream
            var response = (HttpWebResponse)request.GetResponse();

            // Read the response stream
            var responseContent = "";

            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                responseContent = sr.ReadToEnd();
            }

            // Ensure the correct response format
            if (string.IsNullOrEmpty(responseContent) || !responseContent.StartsWith("[")) // we expect a JSON array of exchange data
            {
                throw new Exception("Unexpected API response:\n" + responseContent);
            }

            // Parse the results and return
            List<AmqpExchange> exchanges = new List<AmqpExchange>();
            var items = JSON.Parse(responseContent).AsArray;
            for (var i = 0; i < items.Count; i++) exchanges.Add(AmqpExchange.FromJson(items[i].AsObject));

            return exchanges.ToArray();
        }

        /// <summary>
        /// Gets a list of exchanges for the current connection using an asynchronous request.
        /// </summary>
        /// <remarks>
        /// This method is better suited for Unity 3D since it will not block the game thread while the request is made.
        /// </remarks>
        /// <param name="callback">The callback that will receive the results.</param>
        /// <param name="virtualHost">The optional virtual host to get exchanges for. If NULL the connection's default virtual host is used.</param>
        public void GetExchangesAsync(ExchangeListEventHandler callback, string virtualHost = null)
        {
            if (string.IsNullOrEmpty(Server)) throw new InvalidOperationException("Server cannot be NULL when making a request to its REST API");

            // If not supplied, use the default virtual host
            if (virtualHost == null) virtualHost = VirtualHost;

            // Create the web request for this broker
            var url = (WebPort == 443 ? "https" : "http") + "://" + Server + ":" + WebPort.ToString() + "/api/exchanges/" + virtualHost;
            var request = (HttpWebRequest)WebRequest.Create(url);

            // Add authorization info
            string authInfo = Username + ":" + Password;
            authInfo = Convert.ToBase64String(System.Text.Encoding.Default.GetBytes(authInfo));
            request.Headers["Authorization"] = "Basic " + authInfo;

            // Create a async state object
            var state = new AsyncExchangeListState(request, callback);

            // Start the async request
            request.BeginGetResponse(new AsyncCallback(EndGetExchangesAsync), state);
        }

        // Handles the async result of getting the list of exchanges asynchronously
        private void EndGetExchangesAsync(IAsyncResult result)
        {
            var state = (AsyncExchangeListState)result.AsyncState;
            var response = (HttpWebResponse)state.Request.EndGetResponse(result);

            // Read the response stream
            var responseContent = "";

            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                responseContent = sr.ReadToEnd();
            }

            // Ensure the correct response format
            if (string.IsNullOrEmpty(responseContent) || !responseContent.StartsWith("[")) // we expect a JSON array of exchange data
            {
                throw new Exception("Unexpected API response:\n" + responseContent);
            }

            // Parse the results and return
            List<AmqpExchange> exchanges = new List<AmqpExchange>();
            var items = JSON.Parse(responseContent).AsArray;
            for (var i = 0; i < items.Count; i++) exchanges.Add(AmqpExchange.FromJson(items[i].AsObject));

            // Pass results to callback
            state.Callback(exchanges.ToArray());
        }

        #endregion Exchanges

        #region Queues

        /// <summary>
        /// Declares a queue on the broker for the current virtual host.
        /// </summary>
        /// <param name="name">The name of the queue to declare.</param>
        /// <param name="durable">Whether or not the queue should be durable.</param>
        /// <param name="autoDelete">Whether or not the queue will have auto-delete enabled.</param>
        /// <param name="exclusive">Whether or not the queue is exclusive.</param>
        /// <param name="args">Optional exchange arguments.</param>
        /// <returns>An Exception if one occurred during the operation, otherwise NULL.</returns>
        public Exception DeclareQueue(string name, bool durable = true, bool autoDelete = false, bool exclusive = false, IDictionary<string, object> args = null)
        {
            if (IsConnected) throw new InvalidOperationException("Exchanges cannot be declared when disconnected");
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");

            try
            {
                lock (Channel)
                {
                    Channel.QueueDeclare(name, durable, exclusive, autoDelete, args);
                }
            }
            catch (Exception ex)
            {
                return ex;
            }

            return null;
        }

        /// <summary>
        /// Declares a queue on the broker for the current virtual host.
        /// </summary>
        /// <param name="name">The name of the queue to delete</param>
        /// <param name="ifUnused">Only delete the queue if it is currently unused.</param>
        /// <param name="ifEmpty">Only delete the queue if it is empty.</param>
        /// <returns>An Exception if one occurred during the operation, otherwise NULL.</returns>
        public Exception DeleteQueue(string name, bool ifUnused = false, bool ifEmpty = false)
        {
            if (IsConnected) throw new InvalidOperationException("Exchanges cannot be declared when disconnected");
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");

            try
            {
                lock (Channel)
                {
                    Channel.QueueDelete(name, ifUnused, ifEmpty);
                }
            }
            catch (Exception ex)
            {
                return ex;
            }

            return null;
        }

        /// <summary>
        /// Gets whether or not a queue by a given name exists.
        /// </summary>
        /// <param name="name">The name of the queue to check for.</param>
        /// <param name="virtualHost">The optional virtual host to get queues for. If NULL the connection's default virtual host is used.</param>
        /// <returns>True if the queue exists, False if not.</returns>
        public bool QueueExists(string name, string virtualHost = null)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");

            foreach (var queue in GetQueues(virtualHost))
            {
                if (queue.Name == name) return true;
            }

            return false;
        }

        /// <summary>
        /// Gets a list of queues for the current connection.
        /// </summary>
        /// <param name="virtualHost">The optional virtual host to get queues for. If NULL the connection's default virtual host is used.</param>
        /// <returns>A list of AMQP queues for the current connection.</returns>
        public AmqpQueue[] GetQueues(string virtualHost = null)
        {
            if (string.IsNullOrEmpty(Server)) throw new InvalidOperationException("Server cannot be NULL when making a request to its REST API");

            // If not supplied, use the default virtual host
            if (virtualHost == null) virtualHost = VirtualHost;

            // Create the web request for this broker
            var url = (WebPort == 443 ? "https" : "http") + "://" + Server + ":" + WebPort.ToString() + "/api/queues/" + virtualHost;
            var request = (HttpWebRequest)WebRequest.Create(url);

            // Add authorization info
            string authInfo = Username + ":" + Password;
            authInfo = Convert.ToBase64String(System.Text.Encoding.Default.GetBytes(authInfo));
            request.Headers["Authorization"] = "Basic " + authInfo;

            // Apply the request and retrieve the response stream
            var response = (HttpWebResponse)request.GetResponse();

            // Read the response stream
            var responseContent = "";

            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                responseContent = sr.ReadToEnd();
            }

            // Ensure the correct response format
            if (string.IsNullOrEmpty(responseContent) || !responseContent.StartsWith("[")) // we expect a JSON array of exchange data
            {
                throw new Exception("Unexpected API response:\n" + responseContent);
            }

            // Parse the results and return
            List<AmqpQueue> queues = new List<AmqpQueue>();
            var items = JSON.Parse(responseContent).AsArray;
            for (var i = 0; i < items.Count; i++) queues.Add(AmqpQueue.FromJson(items[i].AsObject));

            return queues.ToArray();
        }

        /// <summary>
        /// Gets a list of queues for the current connection using an asynchronous request.
        /// </summary>
        /// <remarks>
        /// This method is better suited for Unity 3D since it will not block the game thread while the request is made.
        /// </remarks>
        /// <param name="callback">The callback that will receive the results.</param>
        /// <param name="virtualHost">The optional virtual host to get queues for. If NULL the connection's default virtual host is used.</param>
        public void GetQueuesAsync(QueueListEventHandler callback, string virtualHost = null)
        {
            if (string.IsNullOrEmpty(Server)) throw new InvalidOperationException("Server cannot be NULL when making a request to its REST API");

            // If not supplied, use the default virtual host
            if (virtualHost == null) virtualHost = VirtualHost;

            // Create the web request for this broker
            var url = (WebPort == 443 ? "https" : "http") + "://" + Server + ":" + WebPort.ToString() + "/api/queues/" + virtualHost;
            var request = (HttpWebRequest)WebRequest.Create(url);

            // Add authorization info
            string authInfo = Username + ":" + Password;
            authInfo = Convert.ToBase64String(System.Text.Encoding.Default.GetBytes(authInfo));
            request.Headers["Authorization"] = "Basic " + authInfo;

            // Create a async state object
            var state = new AsyncQueueListState(request, callback);

            // Start the async request
            request.BeginGetResponse(new AsyncCallback(EndGetQueuesAsync), state);
        }

        // Handles the async result of getting the list of queues asynchronously
        private void EndGetQueuesAsync(IAsyncResult result)
        {
            var state = (AsyncQueueListState)result.AsyncState;
            var response = (HttpWebResponse)state.Request.EndGetResponse(result);

            // Read the response stream
            var responseContent = "";

            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                responseContent = sr.ReadToEnd();
            }

            // Ensure the correct response format
            if (string.IsNullOrEmpty(responseContent) || !responseContent.StartsWith("[")) // we expect a JSON array of exchange data
            {
                throw new Exception("Unexpected API response:\n" + responseContent);
            }

            // Parse the results and return
            List<AmqpQueue> queues = new List<AmqpQueue>();
            var items = JSON.Parse(responseContent).AsArray;
            for (var i = 0; i < items.Count; i++) queues.Add(AmqpQueue.FromJson(items[i].AsObject));

            // Pass results to callback
            state.Callback(queues.ToArray());
        }

        #endregion Queues

        public override string ToString()
        {
            return AmqpHelper.GetConnectionInfo(this);
        }

        #endregion Methods
    }
}

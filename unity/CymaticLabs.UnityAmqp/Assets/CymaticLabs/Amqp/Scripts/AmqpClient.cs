using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using CymaticLabs.Unity3D.Amqp.SimpleJSON;
using CymaticLabs.Unity3D.Amqp.UI;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// AMQP class used to manage message broker connections and events.
    /// </summary>
    public class AmqpClient : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The name of the connection to use.
        /// </summary>
        [HideInInspector]
        public string Connection;

        /// <summary>
        /// Whether or not to connect to the messaging broker on start.
        /// </summary>
        [Tooltip("Whether or not to connect to the messaging broker on start.")]
        public bool ConnectOnStart = true;

        /// <summary>
        /// Whether or not to use relaxed SSL certificate validation for the broker.
        /// </summary>
        /// <remarks>
        /// This can be useful since Unity's version of Mono (as of 5.x) has a very strict/buggy implementation
        /// around SSL/TLS certificate validation. This can make the connection inherently less secure as it will
        /// trust unverified certificates, but encrypted communications will work. This setting is applied on start 
        /// and updating it during runtime will have no effect.
        /// </remarks>
        [Tooltip("Whether or not to use relaxed SSL certificate validation for the broker. Server SSL certificates will not be verified but encryption will be used.")]
        public bool RelaxedSslValidation = false;

        /// <summary>
        /// Whether or not AMQP events will be written to the AMQP console.
        /// </summary>
        [Tooltip("Whether or not AMQP events will be written to the AMQP console.")]
        public bool WriteToConsole = false;

        /// <summary>
        /// A list of exchange subscriptions to subscribe to when connected.
        /// </summary>
        [Tooltip("A list of exchange subscriptions to subscribe to when connected.")]
        public UnityAmqpExchangeSubscription[] ExchangeSubscriptions;

        /// <summary>
        /// A list of queue subscriptions to subscribe to when connected.
        /// </summary>
        [Tooltip("A list of queue subscriptions to subscribe to when connected.")]
        public UnityAmqpQueueSubscription[] QueueSubscriptions;

        #endregion Inspector

        #region Fields

        // The text asset that contains the AMQP configuration data
        TextAsset configJson;

        // The internal message broker client
        IAmqpBrokerConnection client;

        // Flag used to tell when the client should restore subscriptions
        bool canSubscribe = false;

        // Flag used to tell when the connection to the host was blocked
        bool wasBlocked = false;

        // Flag used to tell when the client has connected to the host
        bool hasConnected = false;

        // Whether or not the client has begun graceful disconnection
        bool isDisconnecting = false;

        // Flag to tell whether when the client has disconnected from the host
        bool hasDisconnected = false;

        // Flag to tell whether when the client has aborted its connection the host
        bool hasAborted = false;

        // Flag to tell when the client is attempting to reconnect to the host
        bool isReconnecting = false;

        // List of available connections
        static List<AmqpConnection> connections = new List<AmqpConnection>();

        // List of exchange-based subscriptions
        List<AmqpExchangeSubscription> exSubscriptions;

        // A queue of incoming exchange-based messages
        Queue<AmqpExchangeReceivedMessage> exMessages;

        // List of queue-based subscriptions
        List<AmqpQueueSubscription> queueSubscriptions;

        // A queue of incoming queue-based messages
        Queue<AmqpQueueReceivedMessage> queueMessages;

        // A queue of incoming unhandled exceptions
        Queue<Exception> connectionExceptions;

        // A queue of incoming unhandled exchange subscribe exceptions
        Queue<Exception> exSubscribeExceptions;

        // A queue of incoming unhandled exchange unsubscribe exceptions
        Queue<Exception> exUnsubscribeExceptions;

        // A queue of incoming unhandled queue subscribe exceptions
        Queue<Exception> queueSubscribeExceptions;

        // A queue of incoming unhandled queue unsubscribe exceptions
        Queue<Exception> queueUnsubscribeExceptions;

        // A queue of exchange subscribes
        Queue<AmqpExchangeSubscription> subscribedExchanges;

        // A queue of exchange unsubscribes
        Queue<AmqpExchangeSubscription> unsubscribedExchanges;

        // A queue of exchange subscribes
        Queue<AmqpQueueSubscription> subscribedQueues;

        // A queue of exchange unsubscribes
        Queue<AmqpQueueSubscription> unsubscribedQueues;

        // A queue of async results when listing AMQP exchanges
        Queue<AsyncExchangeListResult> exchangeListResults;

        // A queue of async results when listing AMQP queues
        Queue<AsyncQueueListResult> queueListResults;

        // Whether or not the application is currently quitting
        bool isQuitting = false;

        /// The message broker host.
        string host;

        /// The message broker AMQP port.
        int amqpPort = 5672;

        /// The message broker web/REST port.
        int webPort = 80;

        /// The message broker's virtual host to use.
        string virtualHost;

        /// The username to use.
        string username;

        /// The password to use.
        string password;

        /// The interval in seconds for reconnection attempts.
        short reconnectInterval = 5;

        /// The requested keep-alive heartbeat in seconds.
        ushort requestedHeartBeat = 30;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Gets the singleton instance of the class.
        /// </summary>
        /// <remarks>
        /// If you used more than one instance of <see cref="AmqpClient"/> then you should ignore
        /// this value is it will always resolve to the last instance to have initialized. This is
        /// convenient for global access to the class when only one instance is being used at a time.
        /// </remarks>
        public static AmqpClient Instance { get; private set; }

        /// <summary>
        /// Gets whether or not the amqp messaging system is currently connected or not.
        /// </summary>
        public bool IsConnected
        {
            get { return client != null ? client.IsConnected : false; }
        }

        /// <summary>
        /// Gets or sets the maximum number of times the client will attempt to reconnect to the host before aborting.
        /// </summary>
        public uint ReconnectRetryLimit
        {
            get { return client != null ? client.ReconnectRetryLimit : uint.MaxValue; }

            set
            {
                client.ReconnectRetryLimit = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of failed subscriptions the client will tolerate before preventing connection to the host.
        /// </summary>
        /// <remarks>
        /// Presently in RabbitMQ if there is an error during subscription the host will close the connection
        /// so the client must reconnect. In cases where the client attempts to resubscribe the same failing subscription
        /// this can lead to an endless loop of connect->subscribe->error->reconnect->infinity. Putting a limit
        /// prevents the loop from going on infinitely.
        /// </remarks>
        public byte SubscribeRetryLimit
        {
            get { return client != null ? client.SubscribeRetryLimit : (byte)10; }

            set
            {
                client.SubscribeRetryLimit = value;
            }
        }

        /// <summary>
        /// The underlying broker connection used by the client.
        /// </summary>
        public IAmqpBrokerConnection BrokerConnection {  get { return client; } }

        /// <summary>
        /// Gets the file name for the AMQP connections data.
        /// </summary>
        public static string ConfigurationFilename
        {
            get { return "AmqpConfiguration.json"; }
        }

        #endregion Properties

        #region Events

        /// <summary>
        /// Occurs when the client has connected to the AMQP message broker.
        /// </summary>
        public AmqpClientUnityEvent OnConnected;

        /// <summary>
        /// Occurs when the client has begun disconnecting from the AMQP message broker, but before it has finished.
        /// </summary>
        public AmqpClientUnityEvent OnDisconnecting;

        /// <summary>
        /// Occurs when the client has disconnected from the AMQP message broker.
        /// </summary>
        public AmqpClientUnityEvent OnDisconnected;

        /// <summary>
        /// Occurs when the client has been blocked by the AMQP message broker.
        /// </summary>
        public AmqpClientUnityEvent OnBlocked;

        /// <summary>
        /// Occurs when the client has started reconnecting to the AMQP message broker.
        /// </summary>
        public AmqpClientUnityEvent OnReconnecting;

        /// <summary>
        /// Occurs when the client connection has aborted and must be manually reset.
        /// </summary>
        public AmqpClientUnityEvent OnConnectionAborted;

        /// <summary>
        /// Occurs when there is a connection error.
        /// </summary>
        public ExceptionUnityEvent OnConnectionError;

        /// <summary>
        /// Occurs when the client subscribes to an exchange.
        /// </summary>
        public AmqpExchangeSubscriptionUnityEvent OnSubscribedToExchange;

        /// <summary>
        /// Occurs when the client unsubscribes from an exchange.
        /// </summary>
        public AmqpExchangeSubscriptionUnityEvent OnUnsubscribedFromExchange;

        /// <summary>
        /// Occurs when the client subscribes to a queue.
        /// </summary>
        public AmqpQueueSubscriptionUnityEvent OnSubscribedToQueue;

        /// <summary>
        /// Occurs when the client unsubscribes from a queue.
        /// </summary>
        public AmqpQueueSubscriptionUnityEvent OnUnsubscribedFromQueue;

        /// <summary>
        /// Occurs when there is an error subscribing to an exchange.
        /// </summary>
        public ExceptionUnityEvent OnExchangeSubscribeError;

        /// <summary>
        /// Occurs when there is an error subscribing to a queue.
        /// </summary>
        public ExceptionUnityEvent OnExchangeUnsubscribeError;

        /// <summary>
        /// Occurs when there is an error unsubscribing from an exchange.
        /// </summary>
        public ExceptionUnityEvent OnQueueSubscribeError;

        /// <summary>
        /// Occurs when there is an error unsubscribing from a queue.
        /// </summary>
        public ExceptionUnityEvent OnQueueUnsubscribeError;

        #endregion Events

        #region Methods

        #region Init

        private void Awake()
        {
            // Initialize fields
            exSubscriptions = new List<AmqpExchangeSubscription>();
            exMessages = new Queue<AmqpExchangeReceivedMessage>();
            queueSubscriptions = new List<AmqpQueueSubscription>();
            queueMessages = new Queue<AmqpQueueReceivedMessage>();
            connectionExceptions = new Queue<Exception>();
            exSubscribeExceptions = new Queue<Exception>();
            exUnsubscribeExceptions = new Queue<Exception>();
            queueSubscribeExceptions = new Queue<Exception>();
            queueUnsubscribeExceptions = new Queue<Exception>();
            subscribedExchanges = new Queue<AmqpExchangeSubscription>();
            unsubscribedExchanges = new Queue<AmqpExchangeSubscription>();
            subscribedQueues = new Queue<AmqpQueueSubscription>();
            unsubscribedQueues = new Queue<AmqpQueueSubscription>();
            connections = new List<AmqpConnection>();
            exchangeListResults = new Queue<AsyncExchangeListResult>();
            queueListResults = new Queue<AsyncQueueListResult>();

            // Assign singleton instance
            Instance = this;

            // If exchange subscriptions were provided through the inspector, add them in
            if (ExchangeSubscriptions != null && ExchangeSubscriptions.Length > 0)
            {
                exSubscriptions.AddRange(ExchangeSubscriptions);
            }

            // If queue subscriptions were provided through the inspector, add them in
            if (QueueSubscriptions != null && QueueSubscriptions.Length > 0)
            {
                queueSubscriptions.AddRange(QueueSubscriptions);
            }

            // Apply SSL settings
            SslHelper.RelaxedValidation = RelaxedSslValidation;

            // Look for connections file
            configJson = Resources.Load<TextAsset>(ConfigurationFilename.Replace(".json", ""));

            if (configJson == null)
            {
                Debug.LogErrorFormat("AMQP JSON configuration asset not found: {0}", ConfigurationFilename);
                return;
            }
            
            // Parse connection JSON data
            try
            {
                var config = JSON.Parse(configJson.text).AsObject;
                var jsonConnections = config["Connections"].AsArray;

                // Populate the connection list from the data
                for (int i = 0; i < jsonConnections.Count; i++)
                {
                    var c = AmqpConnection.FromJsonObject(jsonConnections[i].AsObject);
                    connections.Add(c);
                }
                    
            }
            catch (Exception ex)
            {
                Debug.LogErrorFormat("{0}", ex);
            }
        }

        private void Start()
        {          
            // Connect to host broker on start if configured
            if (ConnectOnStart) Connect();
        }

        #endregion Init

        #region Update

        // Handle Unity update loop
        private void Update()
        {
            #region Process State Change Flags

            /** These flags are set by the thread that the AMQP client runs on and then handled in Unity's game thread **/

            // The client has connected
            if (hasConnected)
            {
                hasConnected = false; // reset the flag for the next event
                Log("Connected to AMQP host {0}", AmqpHelper.GetConnectionInfo(client));
                if (OnConnected != null) OnConnected.Invoke(this);
            }

            // The client has disconnected
            if (hasDisconnected)
            {
                hasDisconnected = false; // reset the flag for the next event
                Log("Disconnected from AMQP host {0}", AmqpHelper.GetConnectionInfo(client));
                if (OnDisconnected != null) OnDisconnected.Invoke(this);
            }

            // Handle client graceful disconnect
            if (isDisconnecting)
            {
                isDisconnecting = false;
                StartCoroutine(DelayDisconnection(2));
            }

            // The client has disconnected
            if (isReconnecting)
            {
                isReconnecting = false; // reset the flag for the next event
                Log("Reconnecting to AMQP host: {0}", AmqpHelper.GetConnectionInfo(client));
                if (OnReconnecting != null) OnReconnecting.Invoke(this);
            }

            // The client has been blocked
            if (wasBlocked)
            {
                wasBlocked = false; // reset the flag for the next event
                AmqpConsole.Color = Color.red;
                Log("Connection to AMQP host blocked: {0}", AmqpHelper.GetConnectionInfo(client));
                AmqpConsole.Color = null;
                if (OnBlocked != null) OnBlocked.Invoke(this);
            }

            // The client connection has aborted
            if (hasAborted)
            {
                hasAborted = false; // reset the flag for the next event
                AmqpConsole.Color = Color.red;
                Log("Connection to AMQP host aborted: {0}", AmqpHelper.GetConnectionInfo(client));
                AmqpConsole.Color = null;
                if (OnConnectionAborted != null) OnConnectionAborted.Invoke(this);
            }

            // It's safe to subscribe so restore subscriptions
            if (canSubscribe && IsConnected)
            {
                canSubscribe = false; // reset the flag for the next event
                RestoreSubscriptions();
            }

            #endregion Process State Change Flags

            if (isQuitting) return;

            #region Process Exceptions

            // Connection Exceptions
            if (connectionExceptions.Count > 0)
            {
                var errors = new Exception[connectionExceptions.Count];

                lock(this)
                {
                    // Copy the list and clear queue
                    connectionExceptions.CopyTo(errors, 0);
                    connectionExceptions.Clear();
                }

                foreach (var ex in errors)
                {
                    // Notify
                    if (OnConnectionError != null) OnConnectionError.Invoke(ex);

                    // Log
                    AmqpConsole.Color = Color.red;
                    Log("{0}", ex);
                    AmqpConsole.Color = null;
                }
            }

            // Exchange Subscribe Exceptions
            if (exSubscribeExceptions.Count > 0)
            {
                var errors = new Exception[exSubscribeExceptions.Count];

                lock (this)
                {
                    // Copy the list and clear queue
                    exSubscribeExceptions.CopyTo(errors, 0);
                    exSubscribeExceptions.Clear();
                }

                foreach (var ex in errors)
                {
                    // Notify
                    if (OnExchangeSubscribeError != null) OnExchangeSubscribeError.Invoke(ex);

                    // Log
                    AmqpConsole.Color = Color.red;
                    Log("{0}", ex);
                    AmqpConsole.Color = null;
                }
            }

            // Exchange Unsubscribe Exceptions
            if (exUnsubscribeExceptions.Count > 0)
            {
                var errors = new Exception[exUnsubscribeExceptions.Count];

                lock (this)
                {
                    // Copy the list and clear queue
                    exUnsubscribeExceptions.CopyTo(errors, 0);
                    exUnsubscribeExceptions.Clear();
                }

                foreach (var ex in errors)
                {
                    // Notify
                    if (OnExchangeUnsubscribeError != null) OnExchangeUnsubscribeError.Invoke(ex);

                    // Log
                    AmqpConsole.Color = Color.red;
                    Log("{0}", ex);
                    AmqpConsole.Color = null;
                }
            }

            // Queue Subscribe Exceptions
            if (exSubscribeExceptions.Count > 0)
            {
                var errors = new Exception[queueSubscribeExceptions.Count];

                lock (this)
                {
                    // Copy the list and clear queue
                    queueSubscribeExceptions.CopyTo(errors, 0);
                    queueSubscribeExceptions.Clear();
                }

                foreach (var ex in errors)
                {
                    // Notify
                    if (OnQueueSubscribeError != null) OnQueueSubscribeError.Invoke(ex);

                    // Log
                    AmqpConsole.Color = Color.red;
                    Log("{0}", ex);
                    AmqpConsole.Color = null;
                }
            }

            // Queue Unsubscribe Exceptions
            if (queueUnsubscribeExceptions.Count > 0)
            {
                var errors = new Exception[queueUnsubscribeExceptions.Count];

                lock (this)
                {
                    // Copy the list and clear queue
                    queueUnsubscribeExceptions.CopyTo(errors, 0);
                    queueUnsubscribeExceptions.Clear();
                }

                foreach (var ex in errors)
                {
                    // Notify
                    if (OnQueueUnsubscribeError != null) OnQueueUnsubscribeError.Invoke(ex);

                    // Log
                    AmqpConsole.Color = Color.red;
                    Log("{0}", ex);
                    AmqpConsole.Color = null;
                }
            }

            #endregion Process Exceptions

            #region Process Subscribes

            // Exchanges
            if (subscribedExchanges.Count > 0)
            {
                var subscriptions = new AmqpExchangeSubscription[subscribedExchanges.Count];

                lock (this)
                {
                    // Copy the list and clear queue
                    subscribedExchanges.CopyTo(subscriptions, 0);
                    subscribedExchanges.Clear();
                }

                foreach (var sub in subscriptions)
                {
                    // Notify
                    if (OnSubscribedToExchange != null) OnSubscribedToExchange.Invoke(sub);

                    // Log
                    Log("Subscribed to exchange: {0}:{1}", sub.ExchangeName, sub.RoutingKey);
                }
            }

            // Queues
            if (subscribedQueues.Count > 0)
            {
                var subscriptions = new AmqpQueueSubscription[subscribedQueues.Count];

                lock (this)
                {
                    // Copy the list and clear queue
                    subscribedQueues.CopyTo(subscriptions, 0);
                    subscribedQueues.Clear();
                }

                foreach (var sub in subscriptions)
                {
                    // Notify
                    if (OnSubscribedToQueue != null) OnSubscribedToQueue.Invoke(sub);

                    // Log
                    Log("Subscribed to queue: {0}:{1}", sub.QueueName, sub.UseAck);
                }
            }

            #endregion Process Subscribes

            #region Process Unsubscribes

            // Exchanges
            if (unsubscribedExchanges.Count > 0)
            {
                var subscriptions = new AmqpExchangeSubscription[unsubscribedExchanges.Count];

                lock (this)
                {
                    // Copy the list and clear queue
                    unsubscribedExchanges.CopyTo(subscriptions, 0);
                    unsubscribedExchanges.Clear();
                }

                foreach (var sub in subscriptions)
                {
                    // Notify
                    if (OnUnsubscribedFromExchange != null) OnUnsubscribedFromExchange.Invoke(sub);

                    // Log
                    Log("Unsubscribed from exchange: {0}:{1}", sub.ExchangeName, sub.RoutingKey);
                }
            }

            // Queues
            if (unsubscribedQueues.Count > 0)
            {
                var subscriptions = new AmqpQueueSubscription[unsubscribedQueues.Count];

                lock (this)
                {
                    // Copy the list and clear queue
                    unsubscribedQueues.CopyTo(subscriptions, 0);
                    unsubscribedQueues.Clear();
                }

                foreach (var sub in subscriptions)
                {
                    // Notify
                    if (OnUnsubscribedFromQueue != null) OnUnsubscribedFromQueue.Invoke(sub);

                    // Log
                    Log("Unsubscribed from queue: {0}:{1}", sub.QueueName, sub.UseAck);
                }
            }

            #endregion Process Unsubscribes

            #region Process Incoming Messages

            // Process exchange messages
            if (exMessages.Count > 0)
            {
                var received = new AmqpExchangeReceivedMessage[exMessages.Count];

                lock (this)
                {
                    // Copy messages to temp list and clear queue
                    exMessages.CopyTo(received, 0);
                    exMessages.Clear();
                }

                // Process messages
                foreach (var rx in received)
                {
                    // Call the non-threadsafe handler, this should be the actual Unity message handler
                    rx.Subscription.Handler(rx);
                }
            }

            // Process queue messages
            if (queueMessages.Count > 0)
            {
                var received = new AmqpQueueReceivedMessage[queueMessages.Count];

                lock (this)
                {
                    // Copy messages to temp list and clear queue
                    queueMessages.CopyTo(received, 0);
                    queueMessages.Clear();
                }

                // Process messages
                foreach (var rx in received)
                {
                    // Call the non-threadsafe handler, this should be the actual Unity message handler
                    rx.Subscription.Handler(rx);
                }
            }

            #endregion Process Incoming Messages

            #region Process Async Exchange Listings

            if (exchangeListResults.Count > 0)
            {
                var results = new AsyncExchangeListResult[exchangeListResults.Count];

                lock (this)
                {
                    // Copy the list and clear queue
                    exchangeListResults.CopyTo(results, 0);
                    exchangeListResults.Clear();
                }

                // Invoke the callbacks and pass the results
                foreach (var result in results) result.Callback(result.ExchangeList);
            }

            #endregion Process Async Exchange Listings

            #region Process Async Queue Listings

            if (queueListResults.Count > 0)
            {
                var results = new AsyncQueueListResult[queueListResults.Count];

                lock (this)
                {
                    // Copy the list and clear queue
                    queueListResults.CopyTo(results, 0);
                    queueListResults.Clear();
                }

                // Invoke the callbacks and pass the results
                foreach (var result in results) result.Callback(result.QueueList);
            }

            #endregion Process Async Queue Listings
        }

        #endregion Update

        #region Clean Up

        // Handles clean-up of AMQP resources when quitting the application
        private void OnApplicationQuit()
        {
            if (isQuitting) return;
            isQuitting = true;
            if (client != null && client.IsConnected) client.Disconnect(); // if not properly disconnected, Unity will hang on quit
        }

        #endregion Clean Up

        #region Connection

        /// <summary>
        /// Gets the current list of connections.
        /// </summary>
        /// <returns>An array of the current AMQP connections.</returns>
        public static AmqpConnection[] GetConnections()
        {
            return connections.ToArray();
        }

        /// <summary>
        /// Gets a connection by name if it exists.
        /// </summary>
        /// <param name="name">The name of the connection to get.</param>
        /// <returns>The connection if it is found, otherwise NULL.</returns>
        public static AmqpConnection GetConnection(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");

            foreach (var c in connections)
            {
                if (c.Name == name) return c;
            }

            return null;
        }

        /// <summary>
        /// Connects to the messaging broker.
        /// </summary>
        public static void Connect()
        {
            if (Instance == null) return;
            Instance.ConnectToHost();
        }

        /// <summary>
        /// Connects to the messaging broker.
        /// </summary>
        public void ConnectToHost()
        {
            if (client != null && client.IsConnected)
            {
                Log("AmqpClient is already connected and cannot reconnect");
                return;
            }

            // Find the connection by name
            var c = GetConnection(Connection);

            if (c != null)
            {
                host = c.Host;
                amqpPort = c.AmqpPort;
                webPort = c.WebPort;
                virtualHost = c.VirtualHost;
                username = c.Username;
                password = c.Password;
                reconnectInterval = c.ReconnectInterval;
                requestedHeartBeat = c.RequestedHeartBeat;
            }

            // Create the client for the connection
            if (client == null)
            {
                client = AmqpConnectionFactory.Create(host, amqpPort, webPort, virtualHost, username, password, reconnectInterval, requestedHeartBeat);
                client.Blocked += Client_Blocked;
                client.Connected += Client_Connected;
                client.Disconnected += Client_Disconnected;
                client.Disconnecting += Client_Disconnecting;
                client.Reconnecting += Client_Reconnecting;
                client.ConnectionError += Client_ConnectionError;
                client.ConnectionAborted += Client_ConnectionAborted;
                client.SubscribedToExchange += Client_SubscribedToExchange;
                client.UnsubscribedFromExchange += Client_UnsubscribedFromExchange;
                client.SubscribedToQueue += Client_SubscribedToQueue;
                client.UnsubscribedFromQueue += Client_UnsubscribedFromQueue;
                client.ExchangeSubscribeError += Client_ExchangeSubscribeError;
                client.ExchangeUnsubscribeError += Client_ExchangeUnsubscribeError;
                client.QueueSubscribeError += Client_QueueSubscribeError;
                client.QueueUnsubscribeError += Client_QueueUnsubscribeError;
            }
            // Or reuse it if possibly
            else
            {
                client.Server = c.Host;
                client.AmqpPort = c.AmqpPort;
                client.WebPort = c.WebPort;
                client.VirtualHost = c.VirtualHost;
                client.Username = username;
                client.Password = password;
                client.ReconnectInterval = c.ReconnectInterval;
                client.RequestedHeartbeat = c.RequestedHeartBeat;
            }

            // Connect the client
            Log("Connecting to AMQP host: {0}", AmqpHelper.GetConnectionInfo(client));
            client.Connect();
        }

        /// <summary>
        /// Disconnects from the messaging broker.
        /// </summary>
        public static void Disconnect()
        {
            if (Instance == null) return;
            Instance.DisconnectFromHost();
        }

        /// <summary>
        /// Disconnects from the messaging broker.
        /// </summary>
        public void DisconnectFromHost()
        {
            if (client == null || !client.IsConnected)
            {
                Log("AmqpClient is not connected and cannot disconnect");
                return;
            }

            // Connect the client
            Log("Disconnecting from AMQP host: {0}", AmqpHelper.GetConnectionInfo(client));
            if (OnDisconnecting != null) OnDisconnecting.Invoke(this);
            isDisconnecting = true;
        }

        // Delays true client disconnectiong by a number of seconds
        IEnumerator DelayDisconnection(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (client != null) client.Disconnect();
        }

        /// <summary>
        /// Resets the connection when it has ended up in an aborted state.
        /// </summary>
        public static void ResetConnection()
        {
            if (Instance == null) return;
            Instance.ResetConnectionToHost();
        }

        /// <summary>
        /// Resets the connection when it has ended up in an aborted state.
        /// </summary>
        public void ResetConnectionToHost()
        {
            if (client == null) return;

            // Connect the client
            Log("Reseting connection for AMQP host: {0}", AmqpHelper.GetConnectionInfo(client));
            client.ResetConnection();
        }

        #endregion Connection

        #region Event Handlers

        // Handles a connection being blocked
        private void Client_Blocked(object sender, System.EventArgs e)
        {
            lock(this)
            {
                wasBlocked = true;
            }
        }

        // Handles when the client connects to the message broker
        private void Client_Connected(object sender, System.EventArgs e)
        {
            lock (this)
            {
                hasConnected= true;
                canSubscribe = true;
            }
        }

        // Handles when the client starts disconnecting
        private void Client_Disconnecting(object sender, System.EventArgs e)
        {
            lock (this)
            {
                isDisconnecting = true;
            }
        }

        // Handles when the client disconnects
        private void Client_Disconnected(object sender, System.EventArgs e)
        {
            lock (this)
            {
                hasDisconnected = true;
            }
        }

        // Handles when the client is trying to reconnect
        private void Client_Reconnecting(object sender, System.EventArgs e)
        {
            lock (this)
            {
                isReconnecting = true;
            }
        }

        // Handles when a connection error occurs
        private void Client_ConnectionError(object sender, ExceptionEventArgs e)
        {
            lock(this)
            {
                connectionExceptions.Enqueue(e.Exception);
            }
        }

        // Handles when the client connection is aborted
        private void Client_ConnectionAborted(object sender, EventArgs e)
        {
            lock(this)
            {
                hasAborted = true;
            }
        }

        // Handles when an exchange is subscribed to
        private void Client_SubscribedToExchange(AmqpExchangeSubscription subscription)
        {
            lock(this)
            {
                subscribedExchanges.Enqueue(subscription);
            }
        }

        // Handles when an exchange is unsubscribed from
        private void Client_UnsubscribedFromExchange(AmqpExchangeSubscription subscription)
        {
            lock (this)
            {
                unsubscribedExchanges.Enqueue(subscription);
            }
        }

        // Handles when a queue is subscribed to
        private void Client_SubscribedToQueue(AmqpQueueSubscription subscription)
        {
            lock (this)
            {
                subscribedQueues.Enqueue(subscription);
            }
        }

        // Handles when a queue is unsubscribed from
        private void Client_UnsubscribedFromQueue(AmqpQueueSubscription subscription)
        {
            lock (this)
            {
                unsubscribedQueues.Enqueue(subscription);
            }
        }

        // Handles when an exchange subscribe error occurs
        private void Client_ExchangeSubscribeError(object sender, ExceptionEventArgs e)
        {
            lock (this)
            {
                exSubscribeExceptions.Enqueue(e.Exception);
            }
        }

        // Handles when an exchange unsubscribe error occurs
        private void Client_ExchangeUnsubscribeError(object sender, ExceptionEventArgs e)
        {
            lock (this)
            {
                exUnsubscribeExceptions.Enqueue(e.Exception);
            }
        }

        // Handles when a queue subscribe error occurs
        private void Client_QueueSubscribeError(object sender, ExceptionEventArgs e)
        {
            lock (this)
            {
                queueSubscribeExceptions.Enqueue(e.Exception);
            }
        }

        // Handles when a queue unsubscribe error occurs
        private void Client_QueueUnsubscribeError(object sender, ExceptionEventArgs e)
        {
            lock (this)
            {
                queueUnsubscribeExceptions.Enqueue(e.Exception);
            }
        }

        // Handles when a message is received from the client
        void Client_ExchangeMessageReceived(AmqpExchangeReceivedMessage received)
        {
            if (isQuitting) return;

            // Enqueue the message for processing
            lock (this)
            {
                exMessages.Enqueue(received);
            }
        }

        // Handles when a message is received from the client
        void Client_QueueMessageReceived(AmqpQueueReceivedMessage received)
        {
            if (isQuitting) return;

            // Enqueue the message for processing
            lock (this)
            {
                queueMessages.Enqueue(received);
            }
        }

        #endregion Event Handlers

        #region Subscriptions

        /// <summary>
        /// Subscribes to a given exchange.
        /// </summary>
        /// <param name="subscription">The exchange subscription to apply.</param>
        public static void Subscribe(AmqpExchangeSubscription subscription)
        {
            if (Instance == null) return;
            Instance.SubscribeToExchange(subscription);
        }

        /// <summary>
        /// Subscribes to a given exchange.
        /// </summary>
        /// <param name="subscription">The exchange subscription to apply.</param>
        public void SubscribeToExchange(AmqpExchangeSubscription subscription)
        {
            if (isQuitting) return;
            if (subscription == null) throw new System.ArgumentNullException("subscription");
            if (exSubscriptions.Contains(subscription)) return;
            exSubscriptions.Add(subscription);

            // Process new subscriptions if we're currently connected
            canSubscribe = true;
        }

        /// <summary>
        /// Subscribes to a given queue.
        /// </summary>
        /// <param name="subscription">The exchange subscription to apply.</param>
        public static void Subscribe(AmqpQueueSubscription subscription)
        {
            if (Instance == null) return;
            Instance.SubscribeToQueue(subscription);
        }

        /// <summary>
        /// Subscribes to a given exchange.
        /// </summary>
        /// <param name="subscription">The exchange subscription to apply.</param>
        public void SubscribeToQueue(AmqpQueueSubscription subscription)
        {
            if (isQuitting) return;
            if (subscription == null) throw new System.ArgumentNullException("subscription");
            if (queueSubscriptions.Contains(subscription)) return;
            queueSubscriptions.Add(subscription);

            // Process new subscriptions if we're currently connected
            canSubscribe = true;
        }

        // Restores the current list of subscriptions
        void RestoreSubscriptions()
        {
            if (isQuitting) return;

            // Process API added subscriptions
            foreach (var sub in exSubscriptions)
            {
                if (!string.IsNullOrEmpty(sub.ConsumerTag)) continue; // already subscribed

                // Assign the thread-safe handler; to interact with Unity's game thread/loop we need this
                sub.ThreadsafeHandler = Client_ExchangeMessageReceived;

                // If this is the Unity subscription type, hook up its Unity event handler
                if (sub is UnityAmqpExchangeSubscription)
                {
                    var usub = (UnityAmqpExchangeSubscription)sub;

                    // Assign a default handler that invokes the UnityEvent
                    if (usub.Handler == null)
                    {
                        usub.Handler = (r) =>
                        {
                            if (usub.Enabled && usub.OnMessageReceived != null) usub.OnMessageReceived.Invoke(r.Subscription, r.Message);
                        };
                    }
                }

                // Subscribe with the message broker
                client.Subscribe(sub);
            }

            foreach (var sub in queueSubscriptions)
            {
                if (!string.IsNullOrEmpty(sub.ConsumerTag)) continue; // already subscribed

                // Assign the thread-safe handler; to interact with Unity's game thread/loop we need this
                sub.ThreadsafeHandler = Client_QueueMessageReceived;

                // If this is the Unity subscription type, hook up its Unity event handler
                if (sub is UnityAmqpQueueSubscription)
                {
                    var usub = (UnityAmqpQueueSubscription)sub;

                    // Assign a default handler that invokes the UnityEvent
                    if (usub.Handler == null)
                    {
                        usub.Handler = (r) =>
                        {
                            if (usub.Enabled && usub.OnMessageReceived != null) usub.OnMessageReceived.Invoke(r.Subscription, r.Message);
                        };
                    }
                }

                // Subscribe with the message broker
                client.Subscribe(sub);
            }
        }

        // Sets up subscriptions on the message broker
        IEnumerator DoRestoreSubscriptions(float delay)
        {
            if (isQuitting) yield break;
            yield return new WaitForSeconds(delay);
            RestoreSubscriptions();
        }

        /// <summary>
        /// unsubscribes from a given exchange.
        /// </summary>
        /// <param name="subscription">The exchange subscription to unsubscribe from.</param>
        public static void Unsubscribe(AmqpExchangeSubscription subscription)
        {
            if (Instance == null) return;
            Instance.UnsubscribeFromExchange(subscription);
        }

        /// <summary>
        /// unsubscribes from a given exchange.
        /// </summary>
        /// <param name="subscription">The exchange subscription to unsubscribe from.</param>
        public void UnsubscribeFromExchange(AmqpExchangeSubscription subscription)
        {
            if (isQuitting) return;
            if (subscription == null) throw new System.ArgumentNullException("subscription");
            if (exSubscriptions.Contains(subscription)) exSubscriptions.Remove(subscription);

            if (client.IsConnected)
            {
                client.Unsubscribe(subscription);
            }
        }

        /// <summary>
        /// unsubscribes from a given queue.
        /// </summary>
        /// <param name="subscription">The queue subscription to unsubscribe from.</param>
        public static void Unsubscribe(AmqpQueueSubscription subscription)
        {
            if (Instance == null) return;
            Instance.UnsubscribeFromQueue(subscription);
        }

        /// <summary>
        /// unsubscribes from a given queue.
        /// </summary>
        /// <param name="subscription">The queue subscription to unsubscribe from.</param>
        public void UnsubscribeFromQueue(AmqpQueueSubscription subscription)
        {
            if (isQuitting) return;
            if (subscription == null) throw new System.ArgumentNullException("subscription");
            if (queueSubscriptions.Contains(subscription)) queueSubscriptions.Remove(subscription);

            if (client.IsConnected)
            {
                client.Unsubscribe(subscription);
            }
        }

        #endregion Subscriptions

        #region Publish

        /// <summary>
        /// Publishes a message on a given exchange.
        /// </summary>
        /// <param name="exchangeName">The name of the exchange to publish the message on.</param>
        /// <param name="routingKey">The optional routing key to use.</param>
        /// <param name="message">The message to publish.</param>
        /// <param name="properties">The optional message properties to use when publishing the message.</param>
        /// <param name="mandatory">Whether or not to publish with the AMQP "mandatory" flag.</param>
        /// <param name="immediate">Whether or not to publish with the AMQP "immediate" flag.</param>
        public static void Publish(string exchangeName, string routingKey, string message, IAmqpMessageProperties properties = null, bool mandatory = false, bool immediate = false)
        {
            if (Instance == null) return;
            Instance.PublishToExchange(exchangeName, routingKey, message, properties, mandatory, immediate);
        }

        /// <summary>
        /// Publishes a message on a given exchange.
        /// </summary>
        /// <param name="exchangeName">The name of the exchange to publish the message on.</param>
        /// <param name="routingKey">The optional routing key to use.</param>
        /// <param name="message">The message to publish.</param>
        /// <param name="properties">The optional message properties to use when publishing the message.</param>
        /// <param name="mandatory">Whether or not to publish with the AMQP "mandatory" flag.</param>
        /// <param name="immediate">Whether or not to publish with the AMQP "immediate" flag.</param>
        public void PublishToExchange(string exchangeName, string routingKey, string message, IAmqpMessageProperties properties = null, bool mandatory = false, bool immediate = false)
        {
            if (isQuitting) return;
            if (string.IsNullOrEmpty(exchangeName)) throw new System.ArgumentNullException("exchangeName");
            if (routingKey == null) routingKey = "";
            if (string.IsNullOrEmpty(message)) throw new System.ArgumentNullException("message");
            if (client == null) throw new System.InvalidOperationException("Must be connected to message broker first.");
            client.Publish(exchangeName, routingKey, properties, message, mandatory, immediate);
        }

        /// <summary>
        /// Publishes a message on a given exchange.
        /// </summary>
        /// <param name="exchangeName">The name of the exchange to publish the message on.</param>
        /// <param name="routingKey">The optional routing key to use.</param>
        /// <param name="message">The message to publish.</param>
        /// <param name="properties">The optional message properties to use when publishing the message.</param>
        /// <param name="mandatory">Whether or not to publish with the AMQP "mandatory" flag.</param>
        /// <param name="immediate">Whether or not to publish with the AMQP "immediate" flag.</param>
        public static void Publish(string exchangeName, string routingKey, byte[] message, IAmqpMessageProperties properties = null, bool mandatory = false, bool immediate = false)
        {
            if (Instance == null) return;
            Instance.PublishToExchange(exchangeName, routingKey, message, properties, mandatory, immediate);
        }

        /// <summary>
        /// Publishes a message on a given exchange.
        /// </summary>
        /// <param name="exchangeName">The name of the exchange to publish the message on.</param>
        /// <param name="routingKey">The optional routing key to use.</param>
        /// <param name="message">The message to publish.</param>
        /// <param name="properties">The optional message properties to use when publishing the message.</param>
        /// <param name="mandatory">Whether or not to publish with the AMQP "mandatory" flag.</param>
        /// <param name="immediate">Whether or not to publish with the AMQP "immediate" flag.</param>
        public void PublishToExchange(string exchangeName, string routingKey, byte[] message, IAmqpMessageProperties properties = null, bool mandatory = false, bool immediate = false)
        {
            if (isQuitting) return;
            if (string.IsNullOrEmpty(exchangeName)) throw new System.ArgumentNullException("exchangeName");
            if (routingKey == null) routingKey = "";
            if (message == null || message.Length == 0) throw new System.ArgumentNullException("message");
            if (client == null) throw new System.InvalidOperationException("Must be connected to message broker first.");
            client.Publish(exchangeName, routingKey, properties, message, mandatory, immediate);
        }

        // TODO Test and support queue publishing scenarios
        ///// <summary>
        ///// Publishes a message to a given queue.
        ///// </summary>
        ///// <param name="quueName">The name of the queue.</param>
        ///// <param name="message">The message to publish.</param>
        ///// <param name="mandatory">Whether or not to publish with the AMQP "mandatory" flag.</param>
        ///// <param name="immediate">Whether or not to publish with the AMQP "immediate" flag.</param>
        //public void PublishToQueue(string quueName, string message, bool mandatory = false, bool immediate = false)
        //{
        //    if (isQuitting) return;
        //    if (string.IsNullOrEmpty(quueName)) throw new System.ArgumentNullException("quueName");
        //    if (string.IsNullOrEmpty(message)) throw new System.ArgumentNullException("message");
        //    if (client == null) throw new System.InvalidOperationException("Must be connected to message broker first.");
        //    client.Publish("", quueName, message, mandatory, immediate);
        //}

        #endregion Publish

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
        public static Exception DeclareExchange(string name, AmqpExchangeTypes type, bool durable = true, bool autoDelete = false, IDictionary<string, object> args = null)
        {
            if (Instance == null) return null;
            return Instance.DeclareExchangeOnHost(name, type, durable, autoDelete, args);
        }

        /// <summary>
        /// Declares an exchange on the broker for the current virtual host.
        /// </summary>
        /// <param name="name">The name of the exchange to declare.</param>
        /// <param name="type">The type of exchange to declare.</param>
        /// <param name="durable">Whether or not the exchange should be durable.</param>
        /// <param name="autoDelete">Whether or not the exchange will have auto-delete enabled.</param>
        /// <param name="args">Optional exchange arguments.</param>
        /// <returns>An Exception if one occurred during the operation, otherwise NULL.</returns>
        public Exception DeclareExchangeOnHost(string name, AmqpExchangeTypes type, bool durable = true, bool autoDelete = false, IDictionary<string, object> args = null)
        {
            return client.DeclareExchange(name, type, durable, autoDelete, args);
        }

        /// <summary>
        /// Declares an exchange on the broker for the current virtual host.
        /// </summary>
        /// <param name="name">The name of the exchange to delete</param>
        /// <param name="ifUnused">Only delete the exchange if it is currently unused.</param>
        /// <returns>An Exception if one occurred during the operation, otherwise NULL.</returns>
        public static Exception DeleteExchange(string name, bool ifUnused = false)
        {
            if (Instance == null) return null;
            return Instance.DeleteExchangeOnHost(name, ifUnused);
        }

        /// <summary>
        /// Declares an exchange on the broker for the current virtual host.
        /// </summary>
        /// <param name="name">The name of the exchange to delete</param>
        /// <param name="ifUnused">Only delete the exchange if it is currently unused.</param>
        /// <returns>An Exception if one occurred during the operation, otherwise NULL.</returns>
        public Exception DeleteExchangeOnHost(string name, bool ifUnused = false)
        {
            return client.DeleteExchange(name, ifUnused);
        }

        /// <summary>
        /// Gets whether or not an exchange by a given name exists.
        /// </summary>
        /// <param name="name">The name of the exchange to check for.</param>
        /// <param name="virtualHost">The optional virtual host to get exchanges for. If NULL the connection's default virtual host is used.</param>
        /// <returns>True if the exchange exists, False if not.</returns>
        public static bool ExchangeExists(string name, string virtualHost = null)
        {
            if (Instance == null) throw new InvalidOperationException("not initialized");
            return Instance.ExchangeExistsOnHost(name, virtualHost);
        }

        /// <summary>
        /// Gets whether or not an exchange by a given name exists.
        /// </summary>
        /// <param name="name">The name of the exchange to check for.</param>
        /// <param name="virtualHost">The optional virtual host to get exchanges for. If NULL the connection's default virtual host is used.</param>
        /// <returns>True if the exchange exists, False if not.</returns>
        public bool ExchangeExistsOnHost(string name, string virtualHost = null)
        {
            return client.ExchangeExists(name, virtualHost);
        }

        /// <summary>
        /// Gets a list of exchanges for the current connection.
        /// </summary>
        /// <param name="virtualHost">The optional virtual host to get exchanges for. If NULL the connection's default virtual host is used.</param>
        /// <returns>A list of AMQP exchanges for the current connection.</returns>
        public static AmqpExchange[] GetExchanges(string virtualHost = null)
        {
            if (Instance == null) return new AmqpExchange[0];
            return Instance.GetExchangeList(virtualHost);
        }

        /// <summary>
        /// Gets a list of exchanges for the current connection.
        /// </summary>
        /// <param name="virtualHost">The optional virtual host to get exchanges for. If NULL the connection's default virtual host is used.</param>
        /// <returns>A list of AMQP exchanges for the current connection.</returns>
        public AmqpExchange[] GetExchangeList(string virtualHost = null)
        {
            if (!IsConnected) return new AmqpExchange[0];
            return client.GetExchanges(virtualHost);
        }

        /// <summary>
        /// Gets a list of exchanges for the current connection using an asynchronous request.
        /// </summary>
        /// <remarks>
        /// This method is better suited for Unity 3D since it will not block the game thread while the request is made.
        /// </remarks>
        /// <param name="callback">The callback that will receive the results.</param>
        /// <param name="virtualHost">The optional virtual host to get exchanges for. If NULL the connection's default virtual host is used.</param>
        public static void GetExchangesAsync(ExchangeListEventHandler callback, string virtualHost = null)
        {
            if (Instance == null)
            {
                if (callback != null) callback(new AmqpExchange[0]);
                return;
            }

            Instance.GetExchangeListAsync(callback, virtualHost);
        }

        /// <summary>
        /// Gets a list of exchanges for the current connection using an asynchronous request.
        /// </summary>
        /// <remarks>
        /// This method is better suited for Unity 3D since it will not block the game thread while the request is made.
        /// </remarks>
        /// <param name="callback">The callback that will receive the results.</param>
        /// <param name="virtualHost">The optional virtual host to get exchanges for. If NULL the connection's default virtual host is used.</param>
        public void GetExchangeListAsync(ExchangeListEventHandler callback, string virtualHost = null)
        {
            if (!IsConnected)
            {
                if (callback != null) callback(new AmqpExchange[0]);
                return;
            }

            client.GetExchangesAsync((exchangeList) =>
            {
                // Queue the results to be handled on the game thread
                lock(this)
                {
                    exchangeListResults.Enqueue(new AsyncExchangeListResult(callback, exchangeList));
                }
            }, virtualHost);
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
        public static Exception DeclareQueue(string name, bool durable = true, bool autoDelete = false, bool exclusive = false, IDictionary<string, object> args = null)
        {
            if (Instance == null) return null;
            return Instance.DeclareQueueOnHost(name, durable, autoDelete, exclusive, args);
        }

        /// <summary>
        /// Declares a queue on the broker for the current virtual host.
        /// </summary>
        /// <param name="name">The name of the queue to declare.</param>
        /// <param name="durable">Whether or not the queue should be durable.</param>
        /// <param name="autoDelete">Whether or not the queue will have auto-delete enabled.</param>
        /// <param name="exclusive">Whether or not the queue is exclusive.</param>
        /// <param name="args">Optional exchange arguments.</param>
        /// <returns>An Exception if one occurred during the operation, otherwise NULL.</returns>
        public Exception DeclareQueueOnHost(string name, bool durable = true, bool autoDelete = false, bool exclusive = false, IDictionary<string, object> args = null)
        {
            return client.DeclareQueue(name, durable, autoDelete, exclusive, args);
        }

        /// <summary>
        /// Declares a queue on the broker for the current virtual host.
        /// </summary>
        /// <param name="name">The name of the queue to delete</param>
        /// <param name="ifUnused">Only delete the queue if it is currently unused.</param>
        /// <param name="ifEmpty">Only delete the queue if it is empty.</param>
        /// <returns>An Exception if one occurred during the operation, otherwise NULL.</returns>
        public static Exception DeleteQueue(string name, bool ifUnused = false, bool ifEmpty = false)
        {
            if (Instance == null) return null;
            return Instance.DeleteQueueOnHost(name, ifUnused, ifEmpty);
        }

        /// <summary>
        /// Declares a queue on the broker for the current virtual host.
        /// </summary>
        /// <param name="name">The name of the queue to delete</param>
        /// <param name="ifUnused">Only delete the queue if it is currently unused.</param>
        /// <param name="ifEmpty">Only delete the queue if it is empty.</param>
        /// <returns>An Exception if one occurred during the operation, otherwise NULL.</returns>
        public Exception DeleteQueueOnHost(string name, bool ifUnused = false, bool ifEmpty = false)
        {
            return client.DeleteQueue(name, ifUnused, ifEmpty);
        }

        /// <summary>
        /// Gets whether or not a queue by a given name exists.
        /// </summary>
        /// <param name="name">The name of the queue to check for.</param>
        /// <param name="virtualHost">The optional virtual host to get queues for. If NULL the connection's default virtual host is used.</param>
        /// <returns>True if the queue exists, False if not.</returns>
        public static bool QueueExists(string name, string virtualHost = null)
        {
            if (Instance == null) throw new InvalidOperationException("not initialized");
            return Instance.QueueExistsOnHost(name, virtualHost);
        }

        /// <summary>
        /// Gets whether or not a queue by a given name exists.
        /// </summary>
        /// <param name="name">The name of the queue to check for.</param>
        /// <param name="virtualHost">The optional virtual host to get queues for. If NULL the connection's default virtual host is used.</param>
        /// <returns>True if the queue exists, False if not.</returns>
        public bool QueueExistsOnHost(string name, string virtualHost = null)
        {
            return client.QueueExists(name, virtualHost);
        }

        /// <summary>
        /// Gets a list of queues for the current connection.
        /// </summary>
        /// <param name="virtualHost">The optional virtual host to get exchanges for. If NULL the connection's default virtual host is used.</param>
        /// <returns>A list of AMQP exchanges for the current connection.</returns>
        public static AmqpQueue[] GetQueues(string virtualHost = null)
        {
            if (Instance == null) return new AmqpQueue[0];
            return Instance.GetQueueList(virtualHost);
        }

        /// <summary>
        /// Gets a list of queues for the current connection.
        /// </summary>
        /// <param name="virtualHost">The optional virtual host to get exchanges for. If NULL the connection's default virtual host is used.</param>
        /// <returns>A list of AMQP exchanges for the current connection.</returns>
        public AmqpQueue[] GetQueueList(string virtualHost = null)
        {
            if (!IsConnected) return new AmqpQueue[0];
            return client.GetQueues(virtualHost);
        }

        /// <summary>
        /// Gets a list of queues for the current connection using an asynchronous request.
        /// </summary>
        /// <remarks>
        /// This method is better suited for Unity 3D since it will not block the game thread while the request is made.
        /// </remarks>
        /// <param name="callback">The callback that will receive the results.</param>
        /// <param name="virtualHost">The optional virtual host to get queues for. If NULL the connection's default virtual host is used.</param>
        public static void GetQueuesAsync(QueueListEventHandler callback, string virtualHost = null)
        {
            if (Instance == null)
            {
                if (callback != null) callback(new AmqpQueue[0]);
                return;
            }

            Instance.GetQueueListAsync(callback, virtualHost);
        }

        /// <summary>
        /// Gets a list of queues for the current connection using an asynchronous request.
        /// </summary>
        /// <remarks>
        /// This method is better suited for Unity 3D since it will not block the game thread while the request is made.
        /// </remarks>
        /// <param name="callback">The callback that will receive the results.</param>
        /// <param name="virtualHost">The optional virtual host to get queues for. If NULL the connection's default virtual host is used.</param>
        public void GetQueueListAsync(QueueListEventHandler callback, string virtualHost = null)
        {
            if (!IsConnected)
            {
                if (callback != null) callback(new AmqpQueue[0]);
                return;
            }

            client.GetQueuesAsync((queueList) =>
            {
                // Queue the results to be handled on the game thread
                lock (this)
                {
                    queueListResults.Enqueue(new AsyncQueueListResult(callback, queueList));
                }
            }, virtualHost);
        }

        #endregion Queues

        #region Logging

        // <summary>
        /// Logs a value to the console.
        /// </summary>
        /// <remarks>If the inspector property <see cref="WriteToConsole"/> is enabled, 
        /// the value will also be written to the <see cref="AmqpConsole"/>.</remarks>
        /// <param name="value">The value to log.</param>
        public static void Log(object value)
        {
            if (Instance == null) return;
            Instance.LogToConsole(value);
        }

        /// <summary>
        /// Logs a value to the console.
        /// </summary>
        /// <remarks>If the inspector property <see cref="WriteToConsole"/> is enabled, 
        /// the value will also be written to the <see cref="AmqpConsole"/>.</remarks>
        /// <param name="value">The value to log.</param>
        public void LogToConsole(object value)
        {
            Debug.Log(value);
            if (WriteToConsole) AmqpConsole.WriteLine(value, true);
        }

        /// <summary>
        /// Logs a value to the console.
        /// </summary>
        /// <remarks>If the inspector property <see cref="WriteToConsole"/> is enabled, 
        /// the value will also be written to the <see cref="AmqpConsole"/>.</remarks>
        /// <param name="text">The text string to format with values.</param>
        /// <param name="values">The values to use in the formatted string.</param>
        public static void Log(string text, params object[] values)
        {
            if (Instance == null) return;
            Instance.LogToConsole(text, values);
        }

        /// <summary>
        /// Logs a value to the console.
        /// </summary>
        /// <remarks>If the inspector property <see cref="WriteToConsole"/> is enabled, 
        /// the value will also be written to the <see cref="AmqpConsole"/>.</remarks>
        /// <param name="text">The text string to format with values.</param>
        /// <param name="values">The values to use in the formatted string.</param>
        public void LogToConsole(string text, params object[] values)
        {
            Debug.LogFormat(text, values);
            if (WriteToConsole) AmqpConsole.WriteLineFormat(text, true, values);
        }

        #endregion Logging

        #region Utility

        /// <summary>
        /// A default message received handler useful for debugging.
        /// </summary>
        /// <param name="subscription">The subscription the message was received on.</param>
        /// <param name="message">The message that was received.</param>
        public void UnityEventDebugExhangeMessageHandler(AmqpExchangeSubscription subscription, IAmqpReceivedMessage message)
        {
            // Decode as text
            var payload = System.Text.Encoding.UTF8.GetString(message.Body);
            AmqpConsole.Color = new Color(1f, 0.5f, 0);
            Log("Message received on {0}: {1}", subscription.ExchangeName + (!string.IsNullOrEmpty(message.RoutingKey) ? ":" + message.RoutingKey : ""), payload);
            AmqpConsole.Color = null;
        }

        /// <summary>
        /// A default message received handler useful for debugging.
        /// </summary>
        /// <param name="subscription">The subscription the message was received on.</param>
        /// <param name="message">The message that was received.</param>
        public void UnityEventDebugQueueMessageHandler(AmqpQueueSubscription subscription, IAmqpReceivedMessage message)
        {
            // Decode as text
            var payload = System.Text.Encoding.UTF8.GetString(message.Body);
            AmqpConsole.Color = new Color(1f, 0.5f, 0);
            Log("Message received on {0}: {1}", subscription.QueueName, payload);
            AmqpConsole.Color = null;
        }

        #endregion Utility

        #endregion Methods
    }
}
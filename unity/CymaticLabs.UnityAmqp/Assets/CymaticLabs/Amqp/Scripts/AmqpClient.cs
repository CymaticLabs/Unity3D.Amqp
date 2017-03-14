using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
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
        /// Whether or not to connect to the messaging broker on start.
        /// </summary>
        public bool ConnectOnStart = true;

        /// <summary>
        /// Whether or not AMQP events will be written to the AMQP console.
        /// </summary>
        public bool WriteToConsole = false;

        /// <summary>
        /// The message broker host.
        /// </summary>
        public string Host;

        /// <summary>
        /// The message broker AMQP port.
        /// </summary>
        public int AmqpPort = 5672;

        /// <summary>
        /// The message broker web/REST port.
        /// </summary>
        public int WebPort = 80;

        /// <summary>
        /// The message broker's virtual host to use.
        /// </summary>
        public string VirtualHost;

        /// <summary>
        /// The username to use.
        /// </summary>
        public string Username;

        /// <summary>
        /// The password to use.
        /// </summary>
        public string Password;

        /// <summary>
        /// The interval in seconds for reconnection attempts.
        /// </summary>
        public short ReconnectInterval = 5;

        /// <summary>
        /// The requested keep-alive heartbeat in seconds.
        /// </summary>
        public ushort RequestedHeartBeat = 30;

        /// <summary>
        /// The list of exchange subscriptions to use.
        /// </summary>
        public UnityAmqpExchangeSubscription[] ExchangeSubscriptions;

        /// <summary>
        /// The list of queue subscriptions to use.
        /// </summary>
        public UnityAmqpQueueSubscription[] QueueSubscriptions;

        #endregion Inspector

        #region Fields

        // The internal message broker client
        IAmqpBrokerConnection client;

        // Flag used to tell when the client should restore subscriptions
        bool canSubscribe = false;

        // Flag used to tell when the connection to the host was blocked
        bool wasBlocked = false;

        // Flag used to tell when the client has connected to the host
        bool hasConnected = false;

        // Flag to tell whether when the client has disconnected from the host
        bool hasDisconnected = false;

        // Flag to tell when the client is attempting to reconnect to the host
        bool isReconnecting = false;

        // List of exchange-based subscriptions
        List<AmqpExchangeSubscription> exSubscriptions;

        // A queue of incoming exchange-based messages
        Queue<AmqpExchangeReceivedMessage> exMessages;

        // List of queue-based subscriptions
        List<AmqpQueueSubscription> queueSubscriptions;

        // A queue of incoming queue-based messages
        Queue<AmqpQueueReceivedMessage> queueMessages;

        // Whether or not the application is currently quitting
        bool isQuitting = false;

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

        #endregion Properties

        #region Events

        /// <summary>
        /// Occurs when the client has connected to the AMQP message broker.
        /// </summary>
        public UnityEvent OnConnected;

        /// <summary>
        /// Occurs when the client has disconnected from the AMQP message broker.
        /// </summary>
        public UnityEvent OnDisconnected;

        /// <summary>
        /// Occurs when the client has been blocked by the AMQP message broker.
        /// </summary>
        public UnityEvent OnBlocked;

        /// <summary>
        /// Occurs when the client has started reconnecting to the AMQP message broker.
        /// </summary>
        public UnityEvent OnReconnecting;

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
                if (OnConnected != null) OnConnected.Invoke();
            }

            // The client has disconnected
            if (hasDisconnected)
            {
                hasDisconnected = false; // reset the flag for the next event
                Log("Disconnected from AMQP host {0}", AmqpHelper.GetConnectionInfo(client));
                if (OnDisconnected != null) OnDisconnected.Invoke();
            }

            // The client has disconnected
            if (isReconnecting)
            {
                isReconnecting = false; // reset the flag for the next event
                Log("Reconnecting to AMQP host: {0}", AmqpHelper.GetConnectionInfo(client));
                if (OnReconnecting != null) OnReconnecting.Invoke();
            }

            // The client has been blocked
            if (wasBlocked)
            {
                wasBlocked = false; // reset the flag for the next event
                Log("Connection to AMQP host blocked: {0}", AmqpHelper.GetConnectionInfo(client));
                if (OnBlocked != null) OnBlocked.Invoke();
            }

            // It's safe to subscribe so restore subscriptions
            if (canSubscribe)
            {
                canSubscribe = false; // reset the flag for the next event
                RestoreSubscriptions();
            }

            #endregion Process State Change Flags

            if (isQuitting) return;

            #region Process Incoming Messages

            // Process exchange messages
            if (exMessages.Count > 0)
            {
                AmqpExchangeReceivedMessage[] received = new AmqpExchangeReceivedMessage[exMessages.Count];

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
                AmqpQueueReceivedMessage[] received = new AmqpQueueReceivedMessage[queueMessages.Count];

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

            // Create the client if it doesn't already exist
            if (client == null)
            {
                client = AmqpConnectionFactory.Create(Host, AmqpPort, WebPort, VirtualHost, Username, Password, ReconnectInterval, RequestedHeartBeat);
                client.Blocked += Client_Blocked;
                client.Connected += Client_Connected;
                client.Disconnected += Client_Disconnected;
                client.Reconnecting += Client_Reconnecting;
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
            if (client == null && !client.IsConnected)
            {
                Log("AmqpClient is not connected and cannot disconnect");
                return;
            }

            // Connect the client
            Log("Disconnecting from AMQP host: {0}", AmqpHelper.GetConnectionInfo(client));
            client.Disconnect();
        }

        #endregion Connection

        #region Connection Handlers

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

        #endregion Connection Handlers

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
            if (client.IsConnected) canSubscribe = true;
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
            if (client.IsConnected) canSubscribe = true;
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
                Log("Listening for messages on exchange: {0}:{1}", sub.ExchangeName, sub.RoutingKey);
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
                Log("Listening for messages on queue: {0}:{1}", sub.QueueName, sub.UseAck);
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
                Log("Unsubscribed from {0}:{1}", subscription.Name, subscription.RoutingKey);
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
        /// <param name="mandatory">Whether or not to publish with the AMQP "mandatory" flag.</param>
        /// <param name="immediate">Whether or not to publish with the AMQP "immediate" flag.</param>
        public static void Publish(string exchangeName, string routingKey, string message, bool mandatory = false, bool immediate = false)
        {
            if (Instance == null) return;
            Instance.PublishToExchange(exchangeName, routingKey, message, mandatory, immediate);
        }

        /// <summary>
        /// Publishes a message on a given exchange.
        /// </summary>
        /// <param name="exchangeName">The name of the exchange to publish the message on.</param>
        /// <param name="routingKey">The optional routing key to use.</param>
        /// <param name="message">The message to publish.</param>
        /// <param name="mandatory">Whether or not to publish with the AMQP "mandatory" flag.</param>
        /// <param name="immediate">Whether or not to publish with the AMQP "immediate" flag.</param>
        public void PublishToExchange(string exchangeName, string routingKey, string message, bool mandatory = false, bool immediate = false)
        {
            if (isQuitting) return;
            if (string.IsNullOrEmpty(exchangeName)) throw new System.ArgumentNullException("exchangeName");
            if (routingKey == null) routingKey = "";
            if (string.IsNullOrEmpty(message)) throw new System.ArgumentNullException("message");
            if (client == null) throw new System.InvalidOperationException("Must be connected to message broker first.");
            client.Publish(exchangeName, routingKey, message, mandatory, immediate);
        }

        /// <summary>
        /// Publishes a message on a given exchange.
        /// </summary>
        /// <param name="exchangeName">The name of the exchange to publish the message on.</param>
        /// <param name="routingKey">The optional routing key to use.</param>
        /// <param name="message">The message to publish.</param>
        /// <param name="mandatory">Whether or not to publish with the AMQP "mandatory" flag.</param>
        /// <param name="immediate">Whether or not to publish with the AMQP "immediate" flag.</param>
        public static void Publish(string exchangeName, string routingKey, byte[] message, bool mandatory = false, bool immediate = false)
        {
            if (Instance == null) return;
            Instance.PublishToExchange(exchangeName, routingKey, message, mandatory, immediate);
        }

        /// <summary>
        /// Publishes a message on a given exchange.
        /// </summary>
        /// <param name="exchangeName">The name of the exchange to publish the message on.</param>
        /// <param name="routingKey">The optional routing key to use.</param>
        /// <param name="message">The message to publish.</param>
        /// <param name="mandatory">Whether or not to publish with the AMQP "mandatory" flag.</param>
        /// <param name="immediate">Whether or not to publish with the AMQP "immediate" flag.</param>
        public void PublishToExchange(string exchangeName, string routingKey, byte[] message, bool mandatory = false, bool immediate = false)
        {
            if (isQuitting) return;
            if (string.IsNullOrEmpty(exchangeName)) throw new System.ArgumentNullException("exchangeName");
            if (routingKey == null) routingKey = "";
            if (message == null || message.Length == 0) throw new System.ArgumentNullException("message");
            if (client == null) throw new System.InvalidOperationException("Must be connected to message broker first.");
            client.Publish(exchangeName, routingKey, message, mandatory, immediate);
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
            Log("Message received on {0} => {1}", subscription.ExchangeName + ":" + subscription.RoutingKey, payload);
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
            Log("Message received on {0} => {1}", subscription.QueueName, payload);
            AmqpConsole.Color = null;
        }

        #endregion Utility

        #endregion Methods
    }
}
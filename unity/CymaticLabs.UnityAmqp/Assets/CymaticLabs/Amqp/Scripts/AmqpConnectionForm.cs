using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CymaticLabs.Unity3D.Amqp.UI
{
    /// <summary>
    /// Performs UI logic for the demo AMQP connection form.
    /// </summary>
    public class AmqpConnectionForm : MonoBehaviour
    {
        #region Inspector

        // Form inputs
        public InputField Host;
        public InputField AmqpPort;
        public InputField WebPort;
        public InputField VirtualHost;
        public InputField Username;
        public InputField Password;
        public Button ConnectButton;
        public Button DisconnectButton;

        public Dropdown ExchangeName;
        public InputField RoutingKey;
        public Button SubscribeButton;
        public Button UnsubscribeButton;

        public Dropdown PublishExchange;
        public InputField PublishRoutingKey;
        public InputField PublishMessage;
        public Button PublishButton;

        #endregion Inspector

        #region Fields

        // List of created exchange subscriptions
        List<AmqpExchangeSubscription> exSubscriptions;

        // The current list of exchanges
        AmqpExchange[] exchanges;

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Methods

        #region Init

        private void Awake()
        {
            exSubscriptions = new List<AmqpExchangeSubscription>();
            if (Host == null) Debug.LogError("AmqpConnectionForm.Host is not assigned");
            if (AmqpPort == null) Debug.LogError("AmqpConnectionForm.AmqpPort is not assigned");
            if (WebPort == null) Debug.LogError("AmqpConnectionForm.WebPort is not assigned");
            if (VirtualHost == null) Debug.LogError("AmqpConnectionForm.VirtualHost is not assigned");
            if (Username == null) Debug.LogError("AmqpConnectionForm.Username is not assigned");
            if (Password == null) Debug.LogError("AmqpConnectionForm.Password is not assigned");
            if (ExchangeName == null) Debug.LogError("AmqpConnectionForm.ExchangeName is not assigned");
            if (RoutingKey == null) Debug.LogError("AmqpConnectionForm.RoutingKey is not assigned");
            if (SubscribeButton == null) Debug.LogError("AmqpConnectionForm.SubscribeButton is not assigned");
            if (UnsubscribeButton == null) Debug.LogError("AmqpConnectionForm.UnsubscribeButton is not assigned");
            if (PublishExchange == null) Debug.LogError("AmqpConnectionForm.PublishExchange is not assigned");
            if (PublishRoutingKey == null) Debug.LogError("AmqpConnectionForm.PublishRoutingKey is not assigned");
            if (PublishMessage == null) Debug.LogError("AmqpConnectionForm.PublishMessage is not assigned");
            if (PublishButton == null) Debug.LogError("AmqpConnectionForm.PublishButton is not assigned");
        }

        private void Start()
        {
            AmqpClient.Instance.OnConnected.AddListener(HandleConnected);
            AmqpClient.Instance.OnDisconnected.AddListener(HandleDisconnected);
            AmqpClient.Instance.OnReconnecting.AddListener(HandleReconnecting);
            AmqpClient.Instance.OnBlocked.AddListener(HandleBlocked);
            AmqpClient.Instance.OnSubscribedToExchange.AddListener(HandleExchangeSubscribed);
            AmqpClient.Instance.OnUnsubscribedFromExchange.AddListener(HandleExchangeUnsubscribed);

            // Copy values from AmqpClient instance
            var i = AmqpClient.Instance;
            Host.text = i.Host;
            AmqpPort.text = i.AmqpPort.ToString();
            WebPort.text = i.WebPort.ToString();
            VirtualHost.text = i.VirtualHost;
            Username.text = i.Username;
            Password.text = i.Password;
        }

        #endregion Init

        #region Update

        #endregion Update

        /// <summary>
        /// Connects to the AMQP broker using the form's client connection values.
        /// </summary>
        public void Connect()
        {
            // Validate args
            var isValid = true;
            int amqpPort = 5672, webPort = 80;

            if (string.IsNullOrEmpty(Host.text))
            {
                isValid = false;
                AmqpConsole.Color = Color.red;
                AmqpConsole.WriteLine("* Host cannot be blank");
                AmqpConsole.Color = null;
            }

            if (string.IsNullOrEmpty(AmqpPort.text))
            {
                isValid = false;
                AmqpConsole.Color = Color.red;
                AmqpConsole.WriteLine("* AMQP Port cannot be blank");
                AmqpConsole.Color = null;
            }

            if (isValid && !int.TryParse(AmqpPort.text, out amqpPort))
            {
                isValid = false;
                AmqpConsole.Color = Color.red;
                AmqpConsole.WriteLineFormat("* AMQP Port is not a valid port number: {0}", AmqpPort.text);
                AmqpConsole.Color = null;
            }

            if (string.IsNullOrEmpty(WebPort.text))
            {
                isValid = false;
                AmqpConsole.Color = Color.red;
                AmqpConsole.WriteLine("* Web Port cannot be blank");
                AmqpConsole.Color = null;
            }

            if (isValid && !int.TryParse(WebPort.text, out webPort))
            {
                isValid = false;
                AmqpConsole.Color = Color.red;
                AmqpConsole.WriteLineFormat("* Web Port is not a valid port number: {0}", WebPort.text);
                AmqpConsole.Color = null;
            }

            // Don't continue if values are invald
            if (!isValid) return;

            // Ensure a default virtual host
            if (string.IsNullOrEmpty(VirtualHost.text))
            {
                VirtualHost.text = "/";
            }

            // Clear subscriptions
            exSubscriptions.Clear();

            // Assign values
            AmqpClient.Instance.Host = Host.text;
            AmqpClient.Instance.AmqpPort = amqpPort;
            AmqpClient.Instance.WebPort = webPort;
            AmqpClient.Instance.VirtualHost = VirtualHost.text;
            AmqpClient.Instance.Username = Username.text;
            AmqpClient.Instance.Password = Password.text;

            // Connect
            ExchangeName.options.Clear();
            PublishExchange.options.Clear();

            AmqpClient.Connect();
            AmqpConsole.Instance.Focus();
        }

        /// <summary>
        /// Disconnects the AMQP client.
        /// </summary>
        public void Disconnect()
        {
            // Disconnect
            AmqpClient.Disconnect();
            AmqpConsole.Instance.Focus();
        }

        /// <summary>
        /// Subscribes to the AMQP exchange subscription using the form's values.
        /// </summary>
        public void Subscribe()
        {
            // Validate args
            var isValid = true;

            var exchangeName = ExchangeName.options[ExchangeName.value].text;

            if (string.IsNullOrEmpty(exchangeName))
            {
                isValid = false;
                AmqpConsole.Color = Color.red;
                AmqpConsole.WriteLine("* Exchange Name cannot be blank");
                AmqpConsole.Color = null;
            }

            // Don't continue if values are invald
            if (!isValid) return;

            var exchangeType = AmqpExchangeTypes.Direct;

            // Find this exchange and get its exchange type
            foreach (var exchange in exchanges)
            {
                if (exchange.Name == exchangeName)
                {
                    exchangeType = exchange.Type;
                    break;
                }
            }

            var routingKey = RoutingKey.text;

            // Ensure this subscription doesn't already exist
            foreach (var sub in exSubscriptions)
            {
                if (sub.ExchangeName == exchangeName && sub.ExchangeType == exchangeType && sub.RoutingKey == routingKey)
                {
                    AmqpConsole.Color = new Color(1f, 0.5f, 0);
                    AmqpConsole.WriteLineFormat("Subscription already exists for exchange {0}:{1}", exchangeName, routingKey);
                    AmqpConsole.Color = null;
                    AmqpConsole.Instance.Focus();
                    return;
                }
            }

            // Create the new subscription
            var subscription = new UnityAmqpExchangeSubscription(exchangeName, exchangeType, routingKey, null, AmqpClient.Instance.UnityEventDebugExhangeMessageHandler);

            // Subscribe on the client
            AmqpClient.Subscribe(subscription);

            AmqpConsole.Instance.Focus();
        }

        /// <summary>
        /// Unsubscribes from the AMQP exchange subscription using the form's values.
        /// </summary>
        public void Unsubscribe()
        {
            // Validate args
            var isValid = true;

            var exchangeName = ExchangeName.options[ExchangeName.value].text;

            if (string.IsNullOrEmpty(exchangeName))
            {
                isValid = false;
                AmqpConsole.Color = Color.red;
                AmqpConsole.WriteLine("* Exchange Name cannot be blank");
                AmqpConsole.Color = null;
            }

            // Don't continue if values are invald
            if (!isValid) return;

            var exchangeType = AmqpExchangeTypes.Direct;

            // Find this exchange and get its exchange type
            foreach (var exchange in exchanges)
            {
                if (exchange.Name == exchangeName)
                {
                    exchangeType = exchange.Type;
                    break;
                }
            }

            var routingKey = RoutingKey.text;

            // Ensure this subscription already exists
            var subs = exSubscriptions.ToArray();

            foreach (var sub in subs)
            {
                if (sub.ExchangeName == exchangeName && sub.ExchangeType == exchangeType && sub.RoutingKey == routingKey)
                {
                    AmqpClient.Unsubscribe(sub);
                    exSubscriptions.Remove(sub);
                    AmqpConsole.Instance.Focus();
                    return;
                }
            }

            AmqpConsole.Color = new Color(1f, 0.5f, 0);
            AmqpConsole.WriteLineFormat("Subscription not found for exchange {0}:{1}", exchangeName, routingKey);
            AmqpConsole.Color = null;

            AmqpConsole.Instance.Focus();
        }

        /// <summary>
        /// Publishes a message to the current exchange using the form's input values.
        /// </summary>
        public void Publish()
        {
            // Validate args
            var isValid = true;

            var exchangeName = PublishExchange.options[PublishExchange.value].text;

            if (string.IsNullOrEmpty(exchangeName))
            {
                isValid = false;
                AmqpConsole.Color = Color.red;
                AmqpConsole.WriteLine("* Exchange Name cannot be blank");
                AmqpConsole.Color = null;
            }

            var message = PublishMessage.text;

            if (string.IsNullOrEmpty(message))
            {
                isValid = false;
                AmqpConsole.Color = Color.red;
                AmqpConsole.WriteLine("* Message cannot be blank");
                AmqpConsole.Color = null;
            }

            // Don't continue if values are invald
            if (!isValid) return;

            var exchangeType = AmqpExchangeTypes.Direct;

            // Find this exchange and get its exchange type
            foreach (var exchange in exchanges)
            {
                if (exchange.Name == exchangeName)
                {
                    exchangeType = exchange.Type;
                    break;
                }
            }

            var routingKey = PublishRoutingKey.text;

            // Publish the message
            AmqpClient.Publish(exchangeName, routingKey, message);
            PublishMessage.text = null; // clear out message

            AmqpConsole.Instance.Focus();
        }

        #region Event Handlers

        // Handles a connection event
        void HandleConnected()
        {
            Host.readOnly = true;
            AmqpPort.readOnly = true;
            WebPort.readOnly = true;
            VirtualHost.readOnly = true;
            Username.readOnly = true;
            Password.readOnly = true;
            ConnectButton.interactable = false;
            DisconnectButton.interactable = true;

            ExchangeName.interactable = true;
            RoutingKey.interactable = true;
            SubscribeButton.interactable = true;
            UnsubscribeButton.interactable = true;

            PublishButton.interactable = true;
            PublishExchange.interactable = true;
            PublishMessage.interactable = true;
            PublishRoutingKey.interactable = true;

            // Query exchange list
            exchanges = AmqpClient.GetExchanges();

            foreach (var exchange in exchanges)
            {
                if (exchange.Name == null || exchange.Name == "/") continue;
                var option = new Dropdown.OptionData(exchange.Name);
                ExchangeName.options.Add(option);
                PublishExchange.options.Add(option);
            }

            if (exchanges.Length > 0)
            {
                ExchangeName.RefreshShownValue();
                PublishExchange.RefreshShownValue();
            }
        }

        // Handles a disconnection event
        void HandleDisconnected()
        {
            Host.readOnly = false;
            AmqpPort.readOnly = false;
            WebPort.readOnly = false;
            VirtualHost.readOnly = false;
            Username.readOnly = false;
            Password.readOnly = false;
            ConnectButton.interactable = true;
            DisconnectButton.interactable = false;

            ExchangeName.interactable = false;
            RoutingKey.interactable = false;
            SubscribeButton.interactable = false;
            UnsubscribeButton.interactable = false;

            PublishButton.interactable = false;
            PublishExchange.interactable = false;
            PublishMessage.interactable = false;
            PublishRoutingKey.interactable = false;
        }

        // Handles a reconnecting event
        void HandleReconnecting()
        {

        }

        // Handles a blocked event
        void HandleBlocked()
        {

        }

        // Handles exchange subscribes
        void HandleExchangeSubscribed(AmqpExchangeSubscription subscription)
        {
            // Add it to the local list
            exSubscriptions.Add(subscription);
        }

        // Handles exchange unsubscribes
        void HandleExchangeUnsubscribed(AmqpExchangeSubscription subscription)
        {
            // Add it to the local list
            exSubscriptions.Remove(subscription);
        }

        #endregion Event Handlers

        #endregion Methods
    }
}



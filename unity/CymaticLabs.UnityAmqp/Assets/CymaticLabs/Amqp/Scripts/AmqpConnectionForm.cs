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
        public InputField ExchangeName;
        public Dropdown ExchangeType;
        public InputField RoutingKey;
        public Button SubscribeButton;
        public Button UnsubscribeButton;

        #endregion Inspector

        #region Fields

        // List of created exchange subscriptions
        List<UnityAmqpExchangeSubscription> exSubscriptions;

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Methods

        #region Init

        private void Awake()
        {
            exSubscriptions = new List<UnityAmqpExchangeSubscription>();
            if (Host == null) Debug.LogError("AmqpConnectionForm.Host is not assigned");
            if (AmqpPort == null) Debug.LogError("AmqpConnectionForm.AmqpPort is not assigned");
            if (WebPort == null) Debug.LogError("AmqpConnectionForm.WebPort is not assigned");
            if (VirtualHost == null) Debug.LogError("AmqpConnectionForm.VirtualHost is not assigned");
            if (Username == null) Debug.LogError("AmqpConnectionForm.Username is not assigned");
            if (Password == null) Debug.LogError("AmqpConnectionForm.Password is not assigned");
            if (ExchangeName == null) Debug.LogError("AmqpConnectionForm.ExchangeName is not assigned");
            if (ExchangeType == null) Debug.LogError("AmqpConnectionForm.ExchangeType is not assigned");
            if (RoutingKey == null) Debug.LogError("AmqpConnectionForm.RoutingKey is not assigned");
            if (SubscribeButton == null) Debug.LogError("AmqpConnectionForm.SubscribeButton is not assigned");
            if (UnsubscribeButton == null) Debug.LogError("AmqpConnectionForm.UnsubscribeButton is not assigned");
        }

        private void Start()
        {
            AmqpClient.Instance.OnConnected.AddListener(HandleConnected);
            AmqpClient.Instance.OnDisconnected.AddListener(HandleDisconnected);
            AmqpClient.Instance.OnReconnecting.AddListener(HandleReconnecting);
            AmqpClient.Instance.OnBlocked.AddListener(HandleBlocked);
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

            // Assign values
            AmqpClient.Instance.Host = Host.text;
            AmqpClient.Instance.AmqpPort = amqpPort;
            AmqpClient.Instance.WebPort = webPort;
            AmqpClient.Instance.VirtualHost = VirtualHost.text;
            AmqpClient.Instance.Username = Username.text;
            AmqpClient.Instance.Password = Password.text;

            // Connect
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

            if (string.IsNullOrEmpty(ExchangeName.text))
            {
                isValid = false;
                AmqpConsole.Color = Color.red;
                AmqpConsole.WriteLine("* Exchange Name cannot be blank");
                AmqpConsole.Color = null;
            }

            // Don't continue if values are invald
            if (!isValid) return;

            var exchangeName = ExchangeName.text;
            var exchangeType = (AmqpExchangeTypes)System.Enum.Parse(typeof(AmqpExchangeTypes), ExchangeType.options[ExchangeType.value].text, true);
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

            // Add it to the local list
            exSubscriptions.Add(subscription);

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

            if (string.IsNullOrEmpty(ExchangeName.text))
            {
                isValid = false;
                AmqpConsole.Color = Color.red;
                AmqpConsole.WriteLine("* Exchange Name cannot be blank");
                AmqpConsole.Color = null;
            }

            // Don't continue if values are invald
            if (!isValid) return;

            var exchangeName = ExchangeName.text;
            var exchangeType = (AmqpExchangeTypes)System.Enum.Parse(typeof(AmqpExchangeTypes), ExchangeType.options[ExchangeType.value].text, true);
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
            ExchangeType.interactable = true;
            RoutingKey.interactable = true;
            SubscribeButton.interactable = true;
            UnsubscribeButton.interactable = true;
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
            ExchangeType.interactable = false;
            RoutingKey.interactable = false;
            SubscribeButton.interactable = false;
            UnsubscribeButton.interactable = false;
        }

        // Handles a reconnecting event
        void HandleReconnecting()
        {

        }

        // Handles a blocked event
        void HandleBlocked()
        {

        }

        #endregion Methods
    }
}



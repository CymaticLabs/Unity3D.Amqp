using System;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// Helpers class for <see cref="IAmqpBrokerConnection"/> instances.
    /// </summary>
    public static class AmqpHelper
    {
        #region Fields

        /// <summary>
        /// The default unencrypted AMQP port.
        /// </summary>
        public const int DefaultUnsecureAmqpPort = 5672;

        /// <summary>
        /// The default encrypted AMQP port.
        /// </summary>
        public const int DefaultSecureAmqpPort = 5671;

        /// <summary>
        /// The default unencrypted web/REST port.
        /// </summary>
        public const int DefaultUnsecureWebPort = 80;

        /// <summary>
        /// The default encrypted web/REST port.
        /// </summary>
        public const int DefaultSecureWebPort = 443;

        /// <summary>
        /// The default AMQP virtual host.
        /// </summary>
        public const string DefaultVirtualHost = "/";

        /// <summary>
        /// The default AMQP exchange name.
        /// </summary>
        public const string DefaultExchangeName = "amp.topic";

        /// <summary>
        /// The default AMQP exchange type.
        /// </summary>
        public const AmqpExchangeTypes DefaultExchangeType = AmqpExchangeTypes.Topic;

        /// <summary>
        /// The default reconnection/retry interval in seconds.
        /// </summary>
        public const short DefaultReconnectInterval = 5;

        /// <summary>
        /// The default requested heartbeat in seconds.
        /// </summary>
        public const ushort DefaultHeartBeat = 30;

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Constructors

        #endregion Constructors

        #region Methods

        /// <summary>
        /// Gets the current connection's information as a string.
        /// </summary>
        /// <param name="brokerConnection">The broker connection to get information for.</param>
        /// <returns>The current connection's information as a formatted string.</returns>
        public static string GetConnectionInfo(IAmqpBrokerConnection brokerConnection)
        {
            if (brokerConnection == null) throw new ArgumentNullException("brokerConnection");
            return string.Format("{0}:{1} vhost:{2}", brokerConnection.Server, brokerConnection.AmqpPort, brokerConnection.VirtualHost);
        }

        #endregion Methods
    }
}

using System;
using CymaticLabs.Unity3D.Amqp.RabbitMq;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// Helper factory class used to create AMQP connections.
    /// </summary>
    public static class AmqpConnectionFactory
    {
        #region Fields

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Methods

        /// <summary>
        /// Creates a new <see cref="IAmqpBrokerConnection">broker connection</see> and returns it.
        /// </summary>
        /// <param name="server">The host server name or IP.</param>
        /// <param name="amqpPort">The host AMQP port number.</param>
        /// <param name="webPort">The host web/REST port number.</param>
        /// <param name="virtualHost">The broker virtual host to use.</param>
        /// <param name="username">The connection username.</param>
        /// <param name="password">The connection password.</param>
        /// <param name="reconnectInterval">The number of seconds to wait before connection retry attempts.</param>
        /// <param name="requestedHeartbeat">The client/server heartbeat in seconds.</param>
        /// <returns>A new AMQP broker connection object.</returns>
        public static IAmqpBrokerConnection Create(string server, int amqpPort, int webPort, string virtualHost, string username, string password, short reconnectInterval = 5, ushort requestedHeartbeat = 30)
        {
            // TODO support more than just RabbitMQ if needed.
            return Create(Guid.NewGuid(), "RabbitMQ Broker Connection", server, amqpPort, webPort, virtualHost, username, password, reconnectInterval, requestedHeartbeat);
        }

        /// <summary>
        /// Creates a new <see cref="IAmqpBrokerConnection">broker connection</see> and returns it.
        /// </summary>
        /// <param name="id">The unique ID to give the connection.</param>
        /// <param name="name">The name to give the connection.</param>
        /// <param name="server">The host server name or IP.</param>
        /// <param name="amqpPort">The host AMQP port number.</param>
        /// <param name="webPort">The host web/REST port number.</param>
        /// <param name="virtualHost">The broker virtual host to use.</param>
        /// <param name="username">The connection username.</param>
        /// <param name="password">The connection password.</param>
        /// <param name="reconnectInterval">The number of seconds to wait before connection retry attempts.</param>
        /// <param name="requestedHeartbeat">The client/server heartbeat in seconds.</param>
        /// <returns>A new AMQP broker connection object.</returns>
        public static IAmqpBrokerConnection Create(Guid id, string name, string server, int amqpPort, int webPort, string virtualHost,
            string username, string password, short reconnectInterval = 5, ushort requestedHeartbeat = 30)
        {
            // TODO support more than just RabbitMQ if needed.
            return new RabbitMqBrokerConnection(server, amqpPort, webPort, virtualHost, username, password, reconnectInterval, requestedHeartbeat);
        }

        #endregion Methods
    }
}

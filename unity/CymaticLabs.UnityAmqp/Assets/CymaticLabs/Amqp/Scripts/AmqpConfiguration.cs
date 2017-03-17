using System;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// Serializable AMQP configuration object.
    /// </summary>
    [Serializable]
    public class AmqpConfiguration
    {
        /// <summary>
        /// The list of AMQP connections.
        /// </summary>
        public AmqpConnection[] Connections;
    }
}


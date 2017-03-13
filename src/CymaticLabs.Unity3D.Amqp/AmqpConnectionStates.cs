namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// Different types of client connection states.
    /// </summary>
    public enum AmqpConnectionStates
    {
        /// <summary>
        /// The client is disconnected.
        /// </summary>
        Disconnected,

        /// <summary>
        /// The client is in the process of disconnecting.
        /// </summary>
        Disconnecting,

        /// <summary>
        /// The client is in the process of connecting.
        /// </summary>
        Connecting,

        /// <summary>
        /// The client is currently connected.
        /// </summary>
        Connected,

        /// <summary>
        /// The client is currently blocked by the server.
        /// </summary>
        Blocked,
    }
}

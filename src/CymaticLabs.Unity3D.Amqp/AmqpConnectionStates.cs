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

        /// <summary>
        /// The client has currently aborted trying to connect to the server.
        /// This requires a call to <see cref="IAmqpBrokerConnection.ResetConnection"/> before
        /// any further connection attempts.
        /// </summary>
        Aborted
    }
}

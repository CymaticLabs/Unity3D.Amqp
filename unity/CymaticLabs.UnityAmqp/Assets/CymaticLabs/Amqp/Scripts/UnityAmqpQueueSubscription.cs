namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// Unity-specific AMQP queue subscription that exposes Unity events for the
    /// subscription's message received handler.
    /// </summary>
    [System.Serializable]
    public class UnityAmqpQueueSubscription : AmqpQueueSubscription
    {
        /// <summary>
        /// Occurs when a message is received by the subscription.
        /// </summary>
        public AmqpQueueMessageReceivedUnityEvent OnMessageReceived;
    }
}

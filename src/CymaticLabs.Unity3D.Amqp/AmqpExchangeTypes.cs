namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// Different types of AMQP exchanges
    /// </summary>
    public enum AmqpExchangeTypes
    {
        /// <summary>
        /// Basic fanout exchange.
        /// </summary>
        Fanout = 1,

        /// <summary>
        /// A topic exchange that supports routing keys with wildcard patterns.
        /// </summary>
        Topic = 2,

        /// <summary>
        /// An exchange that supports routing based on AMQP message header matching.
        /// </summary>
        Headers = 3,

        /// <summary>
        /// A direct exchange that supports direct matching of a single routing key.
        /// </summary>
        Direct = 4,
    }
}

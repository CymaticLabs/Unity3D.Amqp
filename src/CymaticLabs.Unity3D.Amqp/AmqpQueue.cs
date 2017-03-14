using CymaticLabs.Unity3D.Amqp.SimpleJSON;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// Contains information about an AMQP queue.
    /// </summary>
    public class AmqpQueue
    {
        #region Fields

        #endregion Fields

        #region Properties

        /// <summary>
        /// The name of the exchange.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The virtual host that the exchange belongs to.
        /// </summary>
        public string VirtualHost { get; set; }

        /// <summary>
        /// Whether or not the exchange is set for auto-deletion.
        /// </summary>
        public bool AutoDelete { get; set; }

        /// <summary>
        /// Whether or not the exchange is durable.
        /// </summary>
        public bool Durable { get; set; }

        /// <summary>
        /// The exclusive consumer tag.
        /// </summary>
        public string ExclusiveConsumerTag { get; set; }

        /// <summary>
        /// The current state of the queue.
        /// </summary>
        public string State { get; set; }

        /// <summary>
        /// The queue's policy.
        /// </summary>
        public string Policy { get; set; }

        #endregion Properties

        #region Constructors

        #endregion Constructors

        #region Methods

        /// <summary>
        /// Creates a new <see cref="AmqpQueue"/> from its parsed JSON representation.
        /// </summary>
        /// <param name="json">The JSON exchange object.</param>
        /// <returns>A new <see cref="AmqpExchange"/> instance.</returns>
        public static AmqpQueue FromJson(JSONObject json)
        {
            var queue = new AmqpQueue();

            // Extract values
            var name = json["name"].Value;
            if (string.IsNullOrEmpty(name)) name = "/";

            var type = json["type"].Value;

            var vhost = json["vhost"].Value;
            if (string.IsNullOrEmpty(name)) vhost = "/";

            queue.Name = name;
            queue.VirtualHost = vhost;
            queue.AutoDelete = json["auto_delete"].AsBool;
            queue.Durable = json["durable"].AsBool;
            queue.ExclusiveConsumerTag = json["exclusive_consumer_tag"].Value;
            queue.State = json["state"].Value;
            queue.Policy = json["policy"].Value;

            return queue;
        }

        #endregion Methods
    }
}

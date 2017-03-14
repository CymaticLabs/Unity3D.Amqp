using CymaticLabs.Unity3D.Amqp.SimpleJSON;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// Contains information about an AMQP exchange.
    /// </summary>
    public class AmqpExchange
    {
        #region Fields

        #endregion Fields

        #region Properties

        /// <summary>
        /// The name of the exchange.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The type of exchange.
        /// </summary>
        public AmqpExchangeTypes Type { get; set; }

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

        #endregion Properties

        #region Constructors

        #endregion Constructors

        #region Methods

        /// <summary>
        /// Creates a new <see cref="AmqpExchange"/> from its parsed JSON representation.
        /// </summary>
        /// <param name="json">The JSON exchange object.</param>
        /// <returns>A new <see cref="AmqpExchange"/> instance.</returns>
        public static AmqpExchange FromJson(JSONObject json)
        {
            var exchange = new AmqpExchange();

            // Extract values
            var name = json["name"].Value;
            if (string.IsNullOrEmpty(name)) name = "/";

            var type = json["type"].Value;

            var vhost = json["vhost"].Value;
            if (string.IsNullOrEmpty(name)) vhost = "/";

            exchange.Name = name;
            exchange.Type = (AmqpExchangeTypes)System.Enum.Parse(typeof(AmqpExchangeTypes), type, true);
            exchange.VirtualHost = vhost;
            exchange.AutoDelete = json["auto_delete"].AsBool;
            exchange.Durable = json["durable"].AsBool;

            return exchange;
        }

        #endregion Methods
    }
}

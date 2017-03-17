using UnityEngine;
using CymaticLabs.Unity3D.Amqp.SimpleJSON;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// Represents an AMQP connection.
    /// </summary>
    [System.Serializable]
    public class AmqpConnection
    {
        [SerializeField]
        public string Name;

        [SerializeField]
        public string Host;

        [SerializeField]
        public int AmqpPort;

        [SerializeField]
        public int WebPort;

        [SerializeField]
        public string VirtualHost;

        [SerializeField]
        public string Username;

        [SerializeField]
        public string Password;

        [SerializeField]
        public short ReconnectInterval;

        [SerializeField]
        public ushort RequestedHeartBeat;

        /// <summary>
        /// Creates a new AMQP connection from JSON data.
        /// </summary>
        /// <param name="json">The JSON data to create the object from.</param>
        public static AmqpConnection FromJsonObject(JSONObject json)
        {
            var c = new AmqpConnection();
            c.Name = json["Name"].Value;
            c.Host = json["Host"].Value;
            c.AmqpPort = json["AmqpPort"].AsInt;
            c.WebPort = json["WebPort"].AsInt;
            c.VirtualHost = json["VirtualHost"].Value;
            c.Username = json["Username"].Value;
            c.Password = json["Password"].Value;
            c.ReconnectInterval = (short)json["ReconnectInterval"].AsInt;
            c.RequestedHeartBeat = (ushort)json["RequestedHeartBeat"].AsInt;
            return c;
        }

        /// <summary>
        /// Returns the connection as a JSON object.
        /// </summary>
        public JSONObject ToJsonObject()
        {
            var json = new JSONObject();
            json["Name"] = Name;
            json["Host"] = Host;
            json["AmqpPort"] = AmqpPort;
            json["WebPort"] = WebPort;
            json["VirtualHost"] = VirtualHost;
            json["Username"] = Username;
            json["ReconnectInterval"] = ReconnectInterval;
            json["RequestedHeartBeat"] = RequestedHeartBeat;
            return json;
        }
    }
}

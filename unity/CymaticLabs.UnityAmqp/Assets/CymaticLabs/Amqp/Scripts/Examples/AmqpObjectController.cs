using UnityEngine;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// An example script that shows how to control an object's
    /// position, rotation, and scale using AMQP messages.
    /// </summary>
    public class AmqpObjectController : MonoBehaviour
    {
        [Tooltip("An optional ID filter that looks for an 'id' property in the received message. If the ID does not match this value, the message will be ignored.")]
        public string IdFilter;

        [Tooltip("The name of the exchange to subscribe to.")]
        public string ExchangeName;

        [Tooltip("The exchange type for the exchange being subscribed to. It is important to get this value correct as the RabbitMQ client will close a connection if you pass the wrong type for an already declared exchange.")]
        public AmqpExchangeTypes ExchangeType = AmqpExchangeTypes.Topic;

        [Tooltip("The optional routing key to use when subscribing to the exchange. This mostly applies to 'topic' exchanges.")]
        public string RoutingKey;

        [Tooltip("Whether or not to update the object's position.")]
        public bool UpdatePosition = true;

        [Tooltip("Whether or not to update the object's rotation.")]
        public bool UpdateRotation = true;

        [Tooltip("Whether or not to update the object's scale.")]
        public bool UpdateScale = true;

        [Tooltip("If enabled then position and rotation updates will occur in world space. If disabled they will occur in local space.")]
        public bool UpdateInWorldSpace = true;

        [Tooltip("When enabled received messages will be logged to the debug console.")]
        public bool DebugLogMessages = false;

        // *Note*: Only interact with the AMQP library in Start(), not Awake() 
        // since the AmqpClient initializes itself in Awake() and won't be ready yet.
        void Start()
        {
            // Create a new exchange subscription using the inspector values
            var subscription = new AmqpExchangeSubscription(ExchangeName, ExchangeType, RoutingKey, HandleExchangeMessageReceived);

            /*
             * Add the subscription to the client. If you are using multiple AmqpClient instances then
             * using the static methods won't work. In that case add a inspector property of type 'AmqpClient'
             * and assigned a reference to the connection you want to work with and call the 'SubscribeToExchange()'
             * non-static method instead.
             */
            AmqpClient.Subscribe(subscription);
        }

        /**
         * Handles messages receieved from this object's subscription based on the exchange name,
         * exchange type, and routing key used. You could also write an anonymous delegate in line
         * when creating the subscription like: (received) => { Debug.Log(received.Message.Body.Length); }
         */
        void HandleExchangeMessageReceived(AmqpExchangeReceivedMessage received)
        {
            // First convert the message's body, which is a byte array, into a string for parsing the JSON
            var receivedJson = System.Text.Encoding.UTF8.GetString(received.Message.Body);

            // Log if enabled
            if (DebugLogMessages)
            {
                Debug.LogFormat("AMQP message received for {0}{1} => {2}", name, !string.IsNullOrEmpty(IdFilter) ? " id:" + IdFilter : null, receivedJson);
            }
            
            /**
             *  Parse the JSON message
             *  This example uses the SimpleJSON parser which is included in the AMQP library.
             *  You can find out more about this parser here: http://wiki.unity3d.com/index.php/SimpleJSON
            */
            var msg = CymaticLabs.Unity3D.Amqp.SimpleJSON.JSON.Parse(receivedJson);

            // Get the message ID filter, if any
            var id = msg["id"] != null ? msg["id"].Value : null;

            // If an ID exists but it doesn't match the current ID filter then ignore this message
            if (!string.IsNullOrEmpty(IdFilter) && IdFilter != id)
            {
                if (DebugLogMessages)
                {
                    Debug.LogFormat("AMQP message ignored for {0} id:{1} != {2}", name, IdFilter, id);
                }

                return;
            }

            if (UpdatePosition)
            {
                // If the property exists use its value, otherwise just use the current value
                var objPos = UpdateInWorldSpace ? transform.position : transform.localPosition;
                var posX = msg["posX"] != null ? msg["posX"].AsFloat : objPos.x;
                var posY = msg["posY"] != null ? msg["posY"].AsFloat : objPos.y;
                var posZ = msg["posZ"] != null ? msg["posZ"].AsFloat : objPos.z;

                // Update with new values
                if (UpdateInWorldSpace)
                {
                    transform.position = new Vector3(posX, posY, posZ);
                }
                else
                {
                    transform.localPosition = new Vector3(posX, posY, posZ);
                }
            }

            if (UpdateRotation)
            {
                // If the property exists use its value, otherwise just use the current value
                var objRot = UpdateInWorldSpace ? transform.eulerAngles : transform.localEulerAngles;
                var rotX = msg["rotX"] != null ? msg["rotX"].AsFloat : objRot.x;
                var rotY = msg["rotY"] != null ? msg["rotY"].AsFloat : objRot.y;
                var rotZ = msg["rotZ"] != null ? msg["rotZ"].AsFloat : objRot.z;

                // Update with new values
                if (UpdateInWorldSpace)
                {
                    transform.eulerAngles = new Vector3(rotX, rotY, rotZ);
                }
                else
                {
                    transform.localEulerAngles = new Vector3(rotX, rotY, rotZ);
                }
            }

            if (UpdateScale)
            {
                // If the property exists use its value, otherwise just use the current value
                var scaleX = msg["sclX"] != null ? msg["sclX"].AsFloat : transform.localScale.x;
                var scaleY = msg["sclY"] != null ? msg["sclY"].AsFloat : transform.localScale.y;
                var scaleZ = msg["sclZ"] != null ? msg["sclZ"].AsFloat : transform.localScale.z;

                // Update with new values
                transform.localScale = new Vector3(scaleX, scaleY, scaleZ);
            }
        }
    }
}



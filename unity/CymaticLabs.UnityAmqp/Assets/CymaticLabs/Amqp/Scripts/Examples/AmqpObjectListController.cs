using UnityEngine;
using CymaticLabs.Unity3D.Amqp.SimpleJSON;
using System.Collections.Generic;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// An example script that shows how to control an object's
    /// position, rotation, and scale using AMQP messages.
    /// </summary>
    public class AmqpObjectListController : MonoBehaviour
    {
        #region Inspector

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

        #endregion Inspector

        #region Fields

        // Internal look-up table of object references given their AMQP ID
        private Dictionary<string, AmqpObjectControlReference> objectsById;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Gets the static, singleton instance of the behaviour.
        /// </summary>
        public static AmqpObjectListController Instance { get; private set; }

        #endregion Properties

        #region Methods

        private void Awake()
        {
            // Set static instance
            Instance = this;

            // Initialize the object/id look-up table
            objectsById = new Dictionary<string, AmqpObjectControlReference>();
        }

        // *Note*: Only interact with the AMQP library in Start(), not Awake() 
        // since the AmqpClient initializes itself in Awake() and won't be ready yet.
        private void Start()
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

        /// <summary>
        /// Registers a new AMQP object control reference with the controller.
        /// </summary>
        /// <param name="objRef">The object control reference to register.</param>
        public void RegisterObject(AmqpObjectControlReference objRef)
        {
            if (objRef == null) throw new System.ArgumentNullException("objRef");

            // Ensure this reference has been filled out properly
            if (string.IsNullOrEmpty(objRef.AmqpId))
            {
                Debug.LogWarningFormat("AMQP Control Object Reference is missing its ID: {0}", objRef.name);
                return;
            }

            // Add new
            if (!objectsById.ContainsKey(objRef.AmqpId))
            {
                objectsById.Add(objRef.AmqpId, objRef);
            }
            // Replace, but warn
            else
            {
                Debug.LogWarningFormat("AMQP Control Object Reference with ID has already been registered: {0}", objRef.AmqpId);
                objectsById[objRef.AmqpId] = objRef;
            }

            if (DebugLogMessages)
                Debug.LogFormat("AMQP Control Object registered with ID {0} => {1}", objRef.AmqpId, objRef.name);
        }

        /// <summary>
        /// unregisters an existing AMQP object control reference from the controller.
        /// </summary>
        /// <param name="objRef">The object control reference to unregister.</param>
        public void UnregisterObject(AmqpObjectControlReference objRef)
        {
            if (objRef == null) throw new System.ArgumentNullException("objRef");

            // Ensure this reference has been filled out properly
            if (string.IsNullOrEmpty(objRef.AmqpId))
            {
                Debug.LogWarningFormat("AMQP Control Object Reference is missing its ID: {0}", objRef.name);
                return;
            }
           
            if (objectsById.ContainsKey(objRef.AmqpId))
            {
                objectsById.Remove(objRef.AmqpId);
                if (DebugLogMessages) Debug.LogFormat("AMQP Control Object Reference unregistere {0}", objRef.AmqpId);
            }
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

            Debug.Log(receivedJson);

            /**
             *  Parse the JSON message
             *  This example uses the SimpleJSON parser which is included in the AMQP library.
             *  You can find out more about this parser here: http://wiki.unity3d.com/index.php/SimpleJSON
            */

            // If this starts with a bracket, it's an array of messages, so decode separately
            if (receivedJson.StartsWith("["))
            {
                var msgList = JSON.Parse(receivedJson).AsArray;

                for (var i = 0; i < msgList.Count; i++)
                {
                    var msg = msgList[i];
                    UpdateObject(msg);
                }
            }

            // Otherwise it's an individual message so decode individually
            else
            {
                var msg = JSON.Parse(receivedJson);
                UpdateObject(msg);
            }
        }

        // Updates an object in the list with the given update message
        void UpdateObject(JSONNode msg)
        {
            // Get the message ID filter, if any
            var id = msg["id"] != null ? msg["id"].Value : null;

            if (string.IsNullOrEmpty(id))
            {
                if (DebugLogMessages) Debug.LogWarning("AMQP message received without 'id' property.");
                return;
            }

            // Get the object given its message ID
            if (!objectsById.ContainsKey(id))
            {
                if (DebugLogMessages) Debug.LogWarningFormat("No AMQP Object Control Reference found for ID: {0}.", id);
                return;
            }

            // Get the object reference for this ID
            var objRef = objectsById[id];

            if (UpdatePosition)
            {
                // If the property exists use its value, otherwise just use the current value
                var objPos = UpdateInWorldSpace ? objRef.transform.position : objRef.transform.localPosition;
                var posX = msg["posX"] != null ? msg["posX"].AsFloat : objPos.x;
                var posY = msg["posY"] != null ? msg["posY"].AsFloat : objPos.y;
                var posZ = msg["posZ"] != null ? msg["posZ"].AsFloat : objPos.z;

                // Update with new values
                if (UpdateInWorldSpace)
                {
                    objRef.transform.position = new Vector3(posX, posY, posZ);
                }
                else
                {
                    objRef.transform.localPosition = new Vector3(posX, posY, posZ);
                }
            }

            if (UpdateRotation)
            {
                // If the property exists use its value, otherwise just use the current value
                var objRot = UpdateInWorldSpace ? objRef.transform.eulerAngles : objRef.transform.localEulerAngles;
                var rotX = msg["rotX"] != null ? msg["rotX"].AsFloat : objRot.x;
                var rotY = msg["rotY"] != null ? msg["rotY"].AsFloat : objRot.y;
                var rotZ = msg["rotZ"] != null ? msg["rotZ"].AsFloat : objRot.z;

                // Update with new values
                if (UpdateInWorldSpace)
                {
                    objRef.transform.eulerAngles = new Vector3(rotX, rotY, rotZ);
                }
                else
                {
                    objRef.transform.localEulerAngles = new Vector3(rotX, rotY, rotZ);
                }
            }

            if (UpdateScale)
            {
                // If the property exists use its value, otherwise just use the current value
                var scaleX = msg["sclX"] != null ? msg["sclX"].AsFloat : objRef.transform.localScale.x;
                var scaleY = msg["sclY"] != null ? msg["sclY"].AsFloat : objRef.transform.localScale.y;
                var scaleZ = msg["sclZ"] != null ? msg["sclZ"].AsFloat : objRef.transform.localScale.z;

                // Update with new values
                objRef.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);
            }
        }

        #endregion Methods
    }

    ///// <summary>
    ///// Class used to capture references to objects and their IDs from Unity's inspector.
    ///// </summary>
    //[System.Serializable]
    //public class AmqpObjectControlReference
    //{
    //    [Tooltip("The AMQP 'id' of the object.")]
    //    public string Id;

    //    [Tooltip("The target Unity transform to update for this ID.")]
    //    public Transform Target;
    //}
}



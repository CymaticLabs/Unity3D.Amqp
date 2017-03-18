using System.Collections.Generic;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// Contains message properties for a received AMQP message.
    /// </summary>
    public interface IAmqpMessageProperties
    {
        #region Fields

        #endregion Fields

        #region Properties

        /// <summary>
        /// Application ID
        /// </summary>
        string AppId { get; set; }

        /// <summary>
        /// Intra-cluster routing identifier (cluster id is deprecated in AMQP 0-9-1).
        /// </summary>
        string ClusterId { get; set; }

        /// <summary>
        /// MIME content encoding.
        /// </summary>
        string ContentEncoding { get; set; }

        /// <summary>
        /// MIME content type.
        /// </summary>
        string ContentType { get; set; }

        /// <summary>
        /// Application correlation identifier.
        /// </summary>
        string CorrelationId { get; set; }

        /// <summary>
        /// Non-persistent (1) or persistent (2).
        /// </summary>
        byte DeliveryMode { get; set; }

        /// <summary>
        /// Message expiration specification.
        /// </summary>
        string Expiration { get; set; }

        /// <summary>
        /// Message headers dictionary.
        /// </summary>
        IDictionary<string, object> Headers { get; set; }

        /// <summary>
        /// Application message Id.
        /// </summary>
        string MessageId { get; set; }

        /// <summary>
        /// Whether or not the delivery mode is currently set to persistent.
        /// </summary>
        bool Persistent { get; set; }

        /// <summary>
        /// Message priority, 0 to 9.
        /// </summary>
        byte Priority { get; set; }

        /// <summary>
        /// Retrieve the AMQP class ID of this content header.
        /// </summary>
        int ProtocolClassId { get; }

        /// <summary>
        /// Retrieve the AMQP class name of this content header.
        /// </summary>
        string ProtocolClassName { get; }

        /// <summary>
        /// Destination to reply to.
        /// </summary>
        string ReplyTo { get; set; }

        /// <summary>
        /// Message reply to address, if any.
        /// </summary>
        string ReplyToAddress { get; set; }

        /// <summary>
        /// Message timestamp. (Unix time)
        /// </summary>
        long Timestamp { get; set; }

        /// <summary>
        /// Message type name.
        /// </summary>
        string Type { get; set; }

        /// <summary>
        /// User ID.
        /// </summary>
        string UserId { get; set; }

        #endregion Properties

        #region Methods

        #endregion Methods
    }
}

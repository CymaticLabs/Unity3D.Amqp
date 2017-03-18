using System;
using System.Collections.Generic;
using RabbitMQ.Client;

namespace CymaticLabs.Unity3D.Amqp.RabbitMq
{
    /// <summary>
    /// Wraps an <see cref="RabbitMQ.Client.IBasicProperties">RabbitMQ message properties object</see>
    /// in an <see cref="IAmqpMessageProperties"/> interface.
    /// </summary>
    public class RabbitMqMessageProperties : IAmqpMessageProperties
    {
        #region Fields

        // The internal RabbitMQ basic properties reference being wrapped.
        IBasicProperties wrapped;

        // Whether or not the message is persistent
        bool persistent = false;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Application ID
        /// </summary>
        public string AppId
        {
            get
            {
                return wrapped.AppId;
            }

            set
            {
                wrapped.AppId = value;
            }
        }

        /// <summary>
        /// Intra-cluster routing identifier (cluster id is deprecated in AMQP 0-9-1).
        /// </summary>
        public string ClusterId
        {
            get
            {
                return wrapped.ClusterId;
            }

            set
            {
                wrapped.ClusterId = value;
            }
        }

        /// <summary>
        /// MIME content encoding.
        /// </summary>
        public string ContentEncoding
        {
            get
            {
                return wrapped.ContentEncoding;
            }

            set
            {
                wrapped.ContentEncoding = value;
            }
        }

        /// <summary>
        /// MIME content type.
        /// </summary>
        public string ContentType
        {
            get
            {
                return wrapped.ContentType;
            }

            set
            {
                wrapped.ContentType = value;
            }
        }

        /// <summary>
        /// Application correlation identifier.
        /// </summary>
        public string CorrelationId
        {
            get
            {
                return wrapped.CorrelationId;
            }

            set
            {
                wrapped.CorrelationId = value;
            }
        }

        /// <summary>
        /// Non-persistent (1) or persistent (2).
        /// </summary>
        public byte DeliveryMode
        {
            get
            {
                return wrapped.DeliveryMode;
            }

            set
            {
                wrapped.DeliveryMode = value;
            }
        }

        /// <summary>
        /// Message expiration specification.
        /// </summary>
        public string Expiration
        {
            get
            {
                return wrapped.Expiration;
            }

            set
            {
                wrapped.Expiration = value;
            }
        }

        /// <summary>
        /// Message headers dictionary.
        /// </summary>
        public IDictionary<string, object> Headers
        {
            get
            {
                return wrapped.Headers;
            }

            set
            {
                wrapped.Headers = value;
            }
        }

        /// <summary>
        /// Application message Id.
        /// </summary>
        public string MessageId
        {
            get
            {
                return wrapped.MessageId;
            }

            set
            {
                wrapped.MessageId = value;
            }
        }

        /// <summary>
        /// Whether or not the delivery mode is currently set to persistent.
        /// </summary>
        public bool Persistent
        {
            get
            {
                return persistent;
            }

            set
            {
                persistent = value;
                wrapped.SetPersistent(persistent);
            }
        }

        /// <summary>
        /// Message priority, 0 to 9.
        /// </summary>
        public byte Priority
        {
            get
            {
                return wrapped.Priority;
            }

            set
            {
                wrapped.Priority = value;
            }
        }

        /// <summary>
        /// Retrieve the AMQP class ID of this content header.
        /// </summary>
        public int ProtocolClassId
        {
            get
            {
                return wrapped.ProtocolClassId;
            }
        }

        /// <summary>
        /// Retrieve the AMQP class name of this content header.
        /// </summary>
        public string ProtocolClassName
        {
            get
            {
                return wrapped.ProtocolClassName;
            }
        }

        /// <summary>
        /// Destination to reply to.
        /// </summary>
        public string ReplyTo
        {
            get
            {
                return wrapped.ReplyTo;
            }

            set
            {
                wrapped.ReplyTo = value;
            }
        }

        /// <summary>
        /// Message reply to address, if any.
        /// </summary>
        public string ReplyToAddress
        {
            get
            {
                return wrapped.ReplyToAddress.ToString();
            }

            set
            {
                wrapped.ReplyToAddress = PublicationAddress.Parse(value);
            }
        }

        /// <summary>
        /// Message timestamp. (Unix time)
        /// </summary>
        public long Timestamp
        {
            get
            {
                return wrapped.Timestamp.UnixTime;
            }

            set
            {
                wrapped.Timestamp = new AmqpTimestamp(value);
            }
        }

        /// <summary>
        /// Message type name.
        /// </summary>
        public string Type
        {
            get
            {
                return wrapped.Type;
            }

            set
            {
                wrapped.Type = value;
            }
        }

        /// <summary>
        /// User ID.
        /// </summary>
        public string UserId
        {
            get
            {
                return wrapped.UserId;
            }

            set
            {
                wrapped.UserId = value;
            }
        }

        #endregion Properties

        #region Constructors

        /// <summary>
        /// Creates a new instance wrapping the supplied <see cref="RabbitMQ.Client.IBasicProperties"/> instance.
        /// </summary>
        /// <param name="basicProperties">The RabbitMQ properties to wrap.</param>
        public RabbitMqMessageProperties(IBasicProperties basicProperties)
        {
            if (basicProperties == null) throw new ArgumentNullException("basicProperties");
            wrapped = basicProperties;
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// Returns the internal wrapped properties.
        /// </summary>
        /// <returns>The RabbitMQ native <see cref="IBasicProperties">message properties</see>.</returns>
        internal IBasicProperties GetWrapped()
        {
            return wrapped;
        }

        #endregion Methods
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// Base class for AMQP subscriptions.
    /// </summary>
    public abstract class AmqpSubscriptionBase
    {
        #region Fields

        /// <summary>
        /// The name of the subscription.
        /// </summary>
        public string Name;

        /// <summary>
        /// The name of the broker connection the subscription is subscribed on.
        /// </summary>
        public IAmqpBrokerConnection Connection;

        /// <summary>
        /// The consumer tag associated with the subscription.
        /// </summary>
        public string ConsumerTag { get; set; }

        /// <summary>
        /// The optional consumer object associated with the subscription.
        /// </summary>
        public object Consumer { get; set; }

        /// <summary>
        /// Whether or not the subscription has subscriptions enabled and
        /// wants to receive messages.
        /// </summary>
        public bool Enabled = true;

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Constructors

        #endregion Constructors

        #region Methods

        #endregion Methods
    }
}

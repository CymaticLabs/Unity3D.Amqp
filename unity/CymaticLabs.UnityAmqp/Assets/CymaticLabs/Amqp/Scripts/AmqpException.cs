using System;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// For AMQP-specific exceptions.
    /// </summary>
    public class AmqpException : Exception
    {
        #region Fields

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Constructors

        public AmqpException()
        {

        }

        public AmqpException(string message)
            : base(message)
        {

        }

        public AmqpException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        #endregion Constructors

        #region Methods

        #endregion Methods
    }
}

using System;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// Event agurment for events that relate to an exception.
    /// </summary>
    public class ExceptionEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the exception that occurred as part of the event.
        /// </summary>
        public Exception Exception { get; private set; }

        /// <summary>
        /// Creates a new event argument for the given exception.
        /// </summary>
        /// <param name="ex">The exception that occurred.</param>
        public ExceptionEventArgs(Exception ex)
        {
            if (ex == null) throw new ArgumentNullException("ex");
            this.Exception = ex;
        }
    }
}

using System;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// Stores the state of an asynchronous queue list request so it 
    /// can be queued for processing on Unity's game thread.
    /// </summary>
    public class AsyncQueueListResult
    {
        /// <summary>
        /// The async queue list result callback.
        /// </summary>
        public QueueListEventHandler Callback { get; private set; }

        /// <summary>
        /// The list of queues from the request.
        /// </summary>
        public AmqpQueue[] QueueList { get; private set; }

        /// <summary>
        /// Creates a new instance with the given callback and exchange list.
        /// </summary>
        /// <param name="callback">The callback to call.</param>
        /// <param name="queueList">The results of the request.</param>
        public AsyncQueueListResult(QueueListEventHandler callback, AmqpQueue[] queueList)
        {
            if (callback == null) throw new ArgumentNullException("callback");
            if (queueList == null) throw new ArgumentNullException("queueList");
            Callback = callback;
            QueueList = queueList;
        }
    }
}

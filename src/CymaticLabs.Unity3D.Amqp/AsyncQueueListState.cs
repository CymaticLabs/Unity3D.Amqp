using System;
using System.Net;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// A state object used for async web requests when fetching queue data.
    /// </summary>
    public class AsyncQueueListState
    {
        /// <summary>
        /// The async web request being called.
        /// </summary>
        public HttpWebRequest Request { get; private set; }

        /// <summary>
        /// The exchange list callback that will receive the aysnc queue list results.
        /// </summary>
        public QueueListEventHandler Callback { get; private set; }

        /// <summary>
        /// Creates a new state object for the given web request and callback.
        /// </summary>
        /// <param name="request">The web request.</param>
        /// <param name="callback">The results callback.</param>
        public AsyncQueueListState(HttpWebRequest request, QueueListEventHandler callback)
        {
            if (request == null) throw new ArgumentNullException("request");
            if (callback == null) throw new ArgumentNullException("callback");

            Request = request;
            Callback = callback;
        }
    }
}

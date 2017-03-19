using System;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// Stores the state of an asynchronous exchange list request so it 
    /// can be queued for processing on Unity's game thread.
    /// </summary>
    public class AsyncExchangeListResult
    {
        /// <summary>
        /// The async exchange list result callback.
        /// </summary>
        public ExchangeListEventHandler Callback { get; private set; }

        /// <summary>
        /// The list of exchanges from the request.
        /// </summary>
        public AmqpExchange[] ExchangeList { get; private set; }

        /// <summary>
        /// Creates a new instance with the given callback and exchange list.
        /// </summary>
        /// <param name="callback">The callback to call.</param>
        /// <param name="exchangeList">The results of the request.</param>
        public AsyncExchangeListResult(ExchangeListEventHandler callback, AmqpExchange[] exchangeList)
        {
            if (callback == null) throw new ArgumentNullException("callback");
            if (exchangeList == null) throw new ArgumentNullException("exchangeList");
            Callback = callback;
            ExchangeList = exchangeList;
        }
    }
}

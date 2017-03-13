// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2014 GoPivotal, Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//---------------------------------------------------------------------------
//
// The MPL v1.1:
//
//---------------------------------------------------------------------------
//  The contents of this file are subject to the Mozilla Public License
//  Version 1.1 (the "License"); you may not use this file except in
//  compliance with the License. You may obtain a copy of the License
//  at http://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is GoPivotal, Inc.
//  Copyright (c) 2007-2014 GoPivotal, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Util;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Framing;

namespace RabbitMQ.Client.Impl
{
    public abstract class ModelBase : IFullModel, IRecoverable
    {
        private readonly object m_shutdownLock = new object();
        private ModelShutdownEventHandler m_modelShutdown;
        public ShutdownEventArgs m_closeReason = null;

        private readonly object m_eventLock = new object();
        private BasicReturnEventHandler m_basicReturn;
        private BasicAckEventHandler m_basicAck;
        private BasicNackEventHandler m_basicNack;
        private CallbackExceptionEventHandler m_callbackException;
        private FlowControlEventHandler m_flowControl;
        private BasicRecoverOkEventHandler m_basicRecoverOk;
        private IBasicConsumer m_defaultConsumer;

        public ManualResetEvent m_flowControlBlock = new ManualResetEvent(true);
        private readonly object m_flowSendLock = new object();

        private ulong m_nextPubSeqNo = 0;
        private SynchronizedCollection<ulong> m_unconfirmedSet =
            new SynchronizedCollection<ulong>();
        private bool m_onlyAcksReceived = true;

        private RecoveryEventHandler m_recovery;

        public event ModelShutdownEventHandler ModelShutdown
        {
            add
            {
                bool ok = false;
                lock (m_shutdownLock)
                {
                    if (m_closeReason == null)
                    {
                        m_modelShutdown += value;
                        ok = true;
                    }
                }
                if (!ok)
                {
                    value(this, m_closeReason);
                }
            }
            remove
            {
                lock (m_shutdownLock)
                {
                    m_modelShutdown -= value;
                }
            }
        }

        public event BasicReturnEventHandler BasicReturn
        {
            add
            {
                lock (m_eventLock)
                {
                    m_basicReturn += value;
                }
            }
            remove
            {
                lock (m_eventLock)
                {
                    m_basicReturn -= value;
                }
            }
        }

        public event BasicAckEventHandler BasicAcks
        {
            add
            {
                lock (m_eventLock)
                {
                    m_basicAck += value;
                }
            }
            remove
            {
                lock (m_eventLock)
                {
                    m_basicAck -= value;
                }
            }
        }

        public event BasicNackEventHandler BasicNacks
        {
            add
            {
                lock (m_eventLock)
                {
                    m_basicNack += value;
                }
            }
            remove
            {
                lock (m_eventLock)
                {
                    m_basicNack -= value;
                }
            }
        }

        public event CallbackExceptionEventHandler CallbackException
        {
            add
            {
                lock (m_eventLock)
                {
                    m_callbackException += value;
                }
            }
            remove
            {
                lock (m_eventLock)
                {
                    m_callbackException -= value;
                }
            }
        }

        public event FlowControlEventHandler FlowControl
        {
            add
            {
                lock (m_eventLock)
                {
                    m_flowControl += value;
                }
            }
            remove
            {
                lock (m_eventLock)
                {
                    m_flowControl -= value;
                }
            }
        }

        public event BasicRecoverOkEventHandler BasicRecoverOk
        {
            add
            {
                lock (m_eventLock)
                {
                    m_basicRecoverOk += value;
                }
            }
            remove
            {
                lock (m_eventLock)
                {
                    m_basicRecoverOk -= value;
                }
            }
        }

        public event RecoveryEventHandler Recovery
        {
            add
            {
                lock (m_eventLock)
                {
                    m_recovery += value;
                }
            }
            remove
            {
                lock (m_eventLock)
                {
                    m_recovery -= value;
                }
            }
        }

        public IBasicConsumer DefaultConsumer
        {
            get
            {
                return m_defaultConsumer;
            }
            set
            {
                m_defaultConsumer = value;
            }
        }

        public ISession m_session;

        public RpcContinuationQueue m_continuationQueue = new RpcContinuationQueue();

        ///<summary>Only used to kick-start a connection open
        ///sequence. See <see cref="Connection.Open"/> </summary>
        public BlockingCell m_connectionStartCell = null;

        public readonly IDictionary<string, IBasicConsumer> m_consumers = new Dictionary<string, IBasicConsumer>();

        public ModelBase(ISession session)
        {
            m_session = session;
            m_session.CommandReceived = new CommandHandler(HandleCommand);
            m_session.SessionShutdown += new SessionShutdownEventHandler(OnSessionShutdown);
        }

        public ISession Session
        {
            get
            {
                return m_session;
            }
        }

        public int ChannelNumber
        {
            get
            {
                return ((Session)m_session).ChannelNumber;
            }
        }


        public void HandleCommand(ISession session, Command cmd)
        {
            if (DispatchAsynchronous(cmd))
            {
                // Was asynchronous. Already processed. No need to process further.
            }
            else
            {
                m_continuationQueue.Next().HandleCommand(cmd);
            }
        }

        public void OnSessionShutdown(ISession session, ShutdownEventArgs reason)
        {
            SetCloseReason(reason);
            OnModelShutdown(reason);
        }

        public bool SetCloseReason(ShutdownEventArgs reason)
        {
            lock (m_shutdownLock)
            {
                if (m_closeReason == null)
                {
                    m_closeReason = reason;
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        ///<summary>Broadcasts notification of the final shutdown of the model.</summary>
        ///<remarks>
        ///<para>
        ///Do not call anywhere other than at the end of OnSessionShutdown.
        ///</para>
        ///<para>
        ///Must not be called when m_closeReason == null, because
        ///otherwise there's a window when a new continuation could be
        ///being enqueued at the same time as we're broadcasting the
        ///shutdown event. See the definition of Enqueue() above.
        ///</para>
        ///</remarks>
        public virtual void OnModelShutdown(ShutdownEventArgs reason)
        {
            m_continuationQueue.HandleModelShutdown(reason);
            ModelShutdownEventHandler handler;
            lock (m_shutdownLock)
            {
                handler = m_modelShutdown;
                m_modelShutdown = null;
            }
            if (handler != null)
            {
                foreach (ModelShutdownEventHandler h in handler.GetInvocationList()) {
                    try {
                        h(this, reason);
                    } catch (Exception e) {
                        CallbackExceptionEventArgs args = new CallbackExceptionEventArgs(e);
                        args.Detail["context"] = "OnModelShutdown";
                        OnCallbackException(args);
                    }
                }
            }
            lock (m_unconfirmedSet.SyncRoot)
                Monitor.Pulse(m_unconfirmedSet.SyncRoot);
            m_flowControlBlock.Set();
        }

        public virtual void OnBasicReturn(BasicReturnEventArgs args)
        {
            BasicReturnEventHandler handler;
            lock (m_eventLock)
            {
                handler = m_basicReturn;
            }
            if (handler != null)
            {
                foreach (BasicReturnEventHandler h in handler.GetInvocationList()) {
                    try {
                        h(this, args);
                    } catch (Exception e) {
                        CallbackExceptionEventArgs exnArgs = new CallbackExceptionEventArgs(e);
                        exnArgs.Detail["context"] = "OnBasicReturn";
                        OnCallbackException(exnArgs);
                    }
                }
            }
        }

        public virtual void OnBasicAck(BasicAckEventArgs args)
        {
            BasicAckEventHandler handler;
            lock (m_eventLock)
            {
                handler = m_basicAck;
            }
            if (handler != null)
            {
                foreach (BasicAckEventHandler h in handler.GetInvocationList()) {
                    try {
                        h(this, args);
                    } catch (Exception e) {
                        CallbackExceptionEventArgs exnArgs = new CallbackExceptionEventArgs(e);
                        exnArgs.Detail["context"] = "OnBasicAck";
                        OnCallbackException(exnArgs);
                    }
                }
            }

            handleAckNack(args.DeliveryTag, args.Multiple, false);
        }

        public virtual void OnBasicNack(BasicNackEventArgs args)
        {
            BasicNackEventHandler handler;
            lock (m_eventLock)
            {
                handler = m_basicNack;
            }
            if (handler != null)
            {
                foreach (BasicNackEventHandler h in handler.GetInvocationList()) {
                    try {
                        h(this, args);
                    } catch (Exception e) {
                        CallbackExceptionEventArgs exnArgs = new CallbackExceptionEventArgs(e);
                        exnArgs.Detail["context"] = "OnBasicNack";
                        OnCallbackException(exnArgs);
                    }
                }
            }

            handleAckNack(args.DeliveryTag, args.Multiple, true);
        }

        protected virtual void handleAckNack(ulong deliveryTag, bool multiple, bool isNack)
        {
            lock(m_unconfirmedSet.SyncRoot)
            {
                if (multiple) {
                    for (ulong i = (ulong)m_unconfirmedSet[0]; i <= deliveryTag; i++) {
                        // removes potential duplicates
                        while(m_unconfirmedSet.Remove(i))
                        {}
                    }
                } else {
                    while(m_unconfirmedSet.Remove(deliveryTag))
                    {}
                }
                m_onlyAcksReceived = m_onlyAcksReceived && !isNack;
                if (m_unconfirmedSet.Count == 0)
                    Monitor.Pulse(m_unconfirmedSet.SyncRoot);
            }
        }

        public virtual void OnCallbackException(CallbackExceptionEventArgs args)
        {
            CallbackExceptionEventHandler handler;
            lock (m_eventLock) {
                handler = m_callbackException;
            }
            if (handler != null) {
                foreach (CallbackExceptionEventHandler h in handler.GetInvocationList()) {
                    try {
                        h(this, args);
                    } catch {
                        // Exception in
                        // Callback-exception-handler. That was the
                        // app's last chance. Swallow the exception.
                        // FIXME: proper logging
                    }
                }
            }
        }

        public virtual void OnFlowControl(FlowControlEventArgs args)
        {
            FlowControlEventHandler handler;
            lock (m_eventLock)
            {
                handler = m_flowControl;
            }
            if (handler != null)
            {
                foreach (FlowControlEventHandler h in handler.GetInvocationList())
                {
                    try
                    {
                        h(this, args);
                    }
                    catch (Exception e)
                    {
                        CallbackExceptionEventArgs exnArgs = new CallbackExceptionEventArgs(e);
                        exnArgs.Detail["context"] = "OnFlowControl";
                        OnCallbackException(exnArgs);
                    }
                }
            }
        }

        public virtual void OnBasicRecoverOk(EventArgs args)
        {
            BasicRecoverOkEventHandler handler;
            lock (m_eventLock)
            {
                handler = m_basicRecoverOk;
            }
            if (handler != null)
            {
                foreach (BasicRecoverOkEventHandler h in handler.GetInvocationList())
                {
                    try
                    {
                        h(this, args);
                    }
                    catch (Exception e)
                    {
                        CallbackExceptionEventArgs exnArgs = new CallbackExceptionEventArgs(e);
                        exnArgs.Detail["context"] = "OnBasicRecoverOk";
                        OnCallbackException(exnArgs);
                    }
                }
            }
        }

        public void Enqueue(IRpcContinuation k)
        {
            bool ok = false;
            lock (m_shutdownLock)
            {
                if (m_closeReason == null)
                {
                    m_continuationQueue.Enqueue(k);
                    ok = true;
                }
            }
            if (!ok)
            {
                k.HandleModelShutdown(m_closeReason);
            }
        }

        public void TransmitAndEnqueue(Command cmd, IRpcContinuation k)
        {
            Enqueue(k);
            m_session.Transmit(cmd);
        }

        public ShutdownEventArgs CloseReason
        {
            get
            {
                return m_closeReason;
            }
        }

        public bool IsOpen
        {
            get
            {
                return CloseReason == null;
            }
        }

        public bool IsClosed
        {
            get
            {
                return !IsOpen;
            }
        }


        public ulong NextPublishSeqNo
        {
            get
            {
                return m_nextPubSeqNo;
            }
        }

        public void ModelSend(MethodBase method, ContentHeaderBase header, byte[] body)
        {
            if (method.HasContent)
            {
                lock (m_flowSendLock)
                {
                    m_flowControlBlock.WaitOne();
                    m_session.Transmit(new Command(method, header, body));
                }
            }
            else
            {
                m_session.Transmit(new Command(method, header, body));
            }
        }

        public MethodBase ModelRpc(MethodBase method, ContentHeaderBase header, byte[] body)
        {
            SimpleBlockingRpcContinuation k = new SimpleBlockingRpcContinuation();
            TransmitAndEnqueue(new Command(method, header, body), k);
            return k.GetReply().Method;
        }

        public abstract bool DispatchAsynchronous(Command cmd);

        public virtual void HandleBasicDeliver(string consumerTag,
                                               ulong deliveryTag,
                                               bool redelivered,
                                               string exchange,
                                               string routingKey,
                                               IBasicProperties basicProperties,
                                               byte[] body)
        {
            IBasicConsumer consumer;
            lock (m_consumers)
            {
                consumer = (IBasicConsumer)m_consumers[consumerTag];
            }
            if (consumer == null)
            {
                if (DefaultConsumer == null) {
                    throw new InvalidOperationException("Unsolicited delivery -" +
                                                        " see IModel.DefaultConsumer to handle this" +
                                                        " case.");
                }
                else {
                    consumer = DefaultConsumer;
                }
            }

            try {
                consumer.HandleBasicDeliver(consumerTag,
                                            deliveryTag,
                                            redelivered,
                                            exchange,
                                            routingKey,
                                            basicProperties,
                                            body);
            } catch (Exception e) {
                CallbackExceptionEventArgs args = new CallbackExceptionEventArgs(e);
                args.Detail["consumer"] = consumer;
                args.Detail["context"] = "HandleBasicDeliver";
                OnCallbackException(args);
            }
        }

        public void HandleBasicCancel(string consumerTag, bool nowait)
        {
            IBasicConsumer consumer;
            lock (m_consumers)
            {
                consumer = (IBasicConsumer)m_consumers[consumerTag];
                m_consumers.Remove(consumerTag);
            }
            if (consumer == null)
            {
                consumer = DefaultConsumer;
            }

            try {
                consumer.HandleBasicCancel(consumerTag);
            } catch (Exception e) {
                CallbackExceptionEventArgs args = new CallbackExceptionEventArgs(e);
                args.Detail["consumer"] = consumer;
                args.Detail["context"] = "HandleBasicCancel";
                OnCallbackException(args);
            }
        }

        public void HandleBasicReturn(ushort replyCode,
                                      string replyText,
                                      string exchange,
                                      string routingKey,
                                      IBasicProperties basicProperties,
                                      byte[] body)
        {
            BasicReturnEventArgs e = new BasicReturnEventArgs();
            e.ReplyCode = replyCode;
            e.ReplyText = replyText;
            e.Exchange = exchange;
            e.RoutingKey = routingKey;
            e.BasicProperties = basicProperties;
            e.Body = body;
            OnBasicReturn(e);
        }

        public void HandleBasicAck(ulong deliveryTag,
                                   bool multiple)
        {
            BasicAckEventArgs e = new BasicAckEventArgs();
            e.DeliveryTag = deliveryTag;
            e.Multiple = multiple;
            OnBasicAck(e);
        }

        public void HandleBasicNack(ulong deliveryTag,
                                    bool multiple,
                                    bool requeue)
        {
            BasicNackEventArgs e = new BasicNackEventArgs();
            e.DeliveryTag = deliveryTag;
            e.Multiple = multiple;
            e.Requeue = requeue;
            OnBasicNack(e);
        }

        public abstract void _Private_ChannelFlowOk(bool active);

        public void HandleChannelFlow(bool active)
        {
            if (active)
            {
                m_flowControlBlock.Set();
                _Private_ChannelFlowOk(active);
            }
            else
            {
                lock (m_flowSendLock)
                {
                    m_flowControlBlock.Reset();
                    _Private_ChannelFlowOk(active);
                }
            }
            OnFlowControl(new FlowControlEventArgs(active));
        }

        public void HandleConnectionStart(byte versionMajor,
                                          byte versionMinor,
                                          IDictionary<string, object> serverProperties,
                                          byte[] mechanisms,
                                          byte[] locales)
        {
            if (m_connectionStartCell == null)
            {
                ShutdownEventArgs reason =
                    new ShutdownEventArgs(ShutdownInitiator.Library,
                                          Constants.CommandInvalid,
                                          "Unexpected Connection.Start");
                ((Connection)m_session.Connection).Close(reason);
            }
            ConnectionStartDetails details = new ConnectionStartDetails();
            details.m_versionMajor = versionMajor;
            details.m_versionMinor = versionMinor;
            details.m_serverProperties = serverProperties;
            details.m_mechanisms = mechanisms;
            details.m_locales = locales;
            m_connectionStartCell.Value = details;
            m_connectionStartCell = null;
        }

        public void HandleConnectionClose(ushort replyCode,
                                          string replyText,
                                          ushort classId,
                                          ushort methodId)
        {
            ShutdownEventArgs reason = new ShutdownEventArgs(ShutdownInitiator.Peer,
                                                             replyCode,
                                                             replyText,
                                                             classId,
                                                             methodId);
            try
            {
                ((Connection)m_session.Connection).InternalClose(reason);
                _Private_ConnectionCloseOk();
                SetCloseReason((m_session.Connection).CloseReason);
            }
            catch (IOException)
            {
                // Ignored. We're only trying to be polite by sending
                // the close-ok, after all.
            }
            catch (AlreadyClosedException)
            {
                // Ignored. We're only trying to be polite by sending
                // the close-ok, after all.
            }
        }

        public void HandleConnectionBlocked(string reason)
        {
            Connection cb = ((Connection)m_session.Connection);

            cb.HandleConnectionBlocked(reason);
        }

        public void HandleConnectionUnblocked()
        {
            Connection cb = ((Connection)m_session.Connection);

            cb.HandleConnectionUnblocked();
        }

        public void HandleChannelClose(ushort replyCode,
                                       string replyText,
                                       ushort classId,
                                       ushort methodId)
        {
            SetCloseReason(new ShutdownEventArgs(ShutdownInitiator.Peer,
                                                 replyCode,
                                                 replyText,
                                                 classId,
                                                 methodId));

            m_session.Close(m_closeReason, false);
            try {
                _Private_ChannelCloseOk();
            } finally {
                m_session.Notify();
            }
        }

        public void FinishClose()
        {
            if (m_closeReason != null)
            {
                m_session.Close(m_closeReason);
            }
            if (m_connectionStartCell != null)
            {
                m_connectionStartCell.Value = null;
            }
        }

        ///////////////////////////////////////////////////////////////////////////

        public abstract IBasicProperties CreateBasicProperties();

        public void ExchangeDeclare(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object> arguments)
        {
            _Private_ExchangeDeclare(exchange, type, false, durable, autoDelete, false, false, arguments);
        }

        public void ExchangeDeclare(string exchange, string type, bool durable)
        {
            ExchangeDeclare(exchange, type, durable, false, null);
        }

        public void ExchangeDeclare(string exchange, string type)
        {
            ExchangeDeclare(exchange, type, false);
        }

        public void ExchangeDeclarePassive(string exchange)
        {
            _Private_ExchangeDeclare(exchange, "", true, false, false, false, false, null);
        }

        public void ExchangeDeclareNoWait(string exchange,
                                          string type,
                                          bool durable,
                                          bool autoDelete,
                                          IDictionary<string, object> arguments)
        {
            _Private_ExchangeDeclare(exchange, type, false, durable, autoDelete, false, true, arguments);
        }

        public abstract void _Private_ExchangeDeclare(string exchange,
                                                      string type,
                                                      bool passive,
                                                      bool durable,
                                                      bool autoDelete,
                                                      bool @internal,
                                                      bool nowait,
                                                      IDictionary<string, object> arguments);

        public void ExchangeDelete(string exchange,
                                   bool ifUnused)
        {
            _Private_ExchangeDelete(exchange, ifUnused, false);
        }

        public void ExchangeDelete(string exchange)
        {
            ExchangeDelete(exchange, false);
        }

        public void ExchangeDeleteNoWait(string exchange,
                                         bool ifUnused)
        {
            _Private_ExchangeDelete(exchange, ifUnused, false);
        }

        public abstract void _Private_ExchangeDelete(string exchange,
                                                     bool ifUnused,
                                                     bool nowait);

        public void ExchangeBind(string destination,
                                 string source,
                                 string routingKey)
        {
            ExchangeBind(destination, source, routingKey, null);
        }

        public void ExchangeBind(string destination,
                                 string source,
                                 string routingKey,
                                 IDictionary<string, object> arguments)
        {
            _Private_ExchangeBind(destination, source, routingKey, false, arguments);
        }

        public void ExchangeBindNoWait(string destination,
                                       string source,
                                       string routingKey,
                                       IDictionary<string, object> arguments)
        {
            _Private_ExchangeBind(destination, source, routingKey, true, arguments);
        }

        public abstract void _Private_ExchangeBind(string destination,
                                                   string source,
                                                   string routingKey,
                                                   bool nowait,
                                                   IDictionary<string, object> arguments);

        public void ExchangeUnbind(string destination,
                                   string source,
                                   string routingKey,
                                   IDictionary<string, object> arguments)
        {
            _Private_ExchangeUnbind(destination, source, routingKey, false, arguments);
        }

        public void ExchangeUnbind(string destination,
                                   string source,
                                   string routingKey)
        {
            ExchangeUnbind(destination, source, routingKey, null);
        }

        public void ExchangeUnbindNoWait(string destination,
                                         string source,
                                         string routingKey,
                                         IDictionary<string, object> arguments)
        {
            _Private_ExchangeUnbind(destination, source, routingKey, true, arguments);
        }

        public abstract void _Private_ExchangeUnbind(string destination,
                                                     string source,
                                                     string routingKey,
                                                     bool nowait,
                                                     IDictionary<string, object> arguments);

        //TODO: Mark these as virtual, maybe the model has an optimized way
        //      of dealing with missing parameters.
        public QueueDeclareOk QueueDeclare()
        {
            return QueueDeclare("", false, true, true, null);
        }

        public QueueDeclareOk QueueDeclarePassive(string queue)
        {
            return QueueDeclare(queue, true, false, false, false, null);
        }

        public void QueueDeclareNoWait(string queue, bool durable, bool exclusive,
                                       bool autoDelete, IDictionary<string, object> arguments)
        {
            _Private_QueueDeclare(queue, false, durable, exclusive, autoDelete, true, arguments);
        }

        public QueueDeclareOk QueueDeclare(string queue, bool durable, bool exclusive,
                                           bool autoDelete, IDictionary<string, object> arguments)
        {
            return QueueDeclare(queue, false, durable, exclusive, autoDelete, arguments);
        }

        public class QueueDeclareRpcContinuation : SimpleBlockingRpcContinuation
        {
            public QueueDeclareOk m_result;
            public QueueDeclareRpcContinuation() { }
        }

        private QueueDeclareOk QueueDeclare(string queue, bool passive, bool durable, bool exclusive,
                                            bool autoDelete, IDictionary<string, object> arguments)
        {
            QueueDeclareRpcContinuation k = new QueueDeclareRpcContinuation();
            Enqueue(k);
            _Private_QueueDeclare(queue, passive, durable, exclusive, autoDelete, false, arguments);
            k.GetReply();
            return k.m_result;
        }

        public abstract void _Private_QueueDeclare(string queue,
                                                   bool passive,
                                                   bool durable,
                                                   bool exclusive,
                                                   bool autoDelete,
                                                   bool nowait,
                                                   IDictionary<string, object> arguments);

        public void QueueBind(string queue,
                              string exchange,
                              string routingKey,
                              IDictionary<string, object> arguments)
        {
            _Private_QueueBind(queue, exchange, routingKey, false, arguments);
        }

        public void QueueBind(string queue,
                              string exchange,
                              string routingKey)
        {
            QueueBind(queue, exchange, routingKey, null);
        }

        public void QueueBindNoWait(string queue,
                                    string exchange,
                                    string routingKey,
                                    IDictionary<string, object> arguments)
        {
            _Private_QueueBind(queue, exchange, routingKey, true, arguments);
        }

        public abstract void _Private_QueueBind(string queue,
                                                string exchange,
                                                string routingKey,
                                                bool nowait,
                                                IDictionary<string, object> arguments);

        public abstract void QueueUnbind(string queue,
                                         string exchange,
                                         string routingKey,
                                         IDictionary<string, object> arguments);

        public uint QueuePurge(string queue)
        {
            return _Private_QueuePurge(queue, false);
        }

        public abstract uint _Private_QueuePurge(string queue,
                                                 bool nowait);


        public uint QueueDelete(string queue,
                                bool ifUnused,
                                bool ifEmpty)
        {
            return _Private_QueueDelete(queue, ifUnused, ifEmpty, false);
        }

        public uint QueueDelete(string queue)
        {
            return QueueDelete(queue, false, false);
        }

        public void QueueDeleteNoWait(string queue,
                                      bool ifUnused,
                                      bool ifEmpty)
        {
            _Private_QueueDelete(queue, ifUnused, ifEmpty, true);
        }

        public abstract uint _Private_QueueDelete(string queue,
                                                  bool ifUnused,
                                                  bool ifEmpty,
                                                  bool nowait);

        public void ConfirmSelect()
        {
            if (m_nextPubSeqNo == 0UL)
            {
                m_nextPubSeqNo = 1;
            }
            _Private_ConfirmSelect(false);
        }

        public bool WaitForConfirms(TimeSpan timeout, out bool timedOut)
        {
            if (m_nextPubSeqNo == 0UL) {
                throw new InvalidOperationException("Confirms not selected");
            }
            bool isWaitInfinite = (timeout.TotalMilliseconds == Timeout.Infinite);
            Stopwatch stopwatch = Stopwatch.StartNew();
            lock (m_unconfirmedSet.SyncRoot)
            {
                while (true)
                {
                    if (!IsOpen)
                        throw new AlreadyClosedException(CloseReason);

                    if (m_unconfirmedSet.Count == 0)
                    {
                        bool aux = m_onlyAcksReceived;
                        m_onlyAcksReceived = true;
                        timedOut = false;
                        return aux;
                    }
                    if (isWaitInfinite)
                        Monitor.Wait(m_unconfirmedSet.SyncRoot);
                    else
                    {
                        TimeSpan elapsed = stopwatch.Elapsed;
                        if(elapsed > timeout || !Monitor.Wait(
                                                              m_unconfirmedSet.SyncRoot, timeout - elapsed))
                        {
                            timedOut = true;
                            return true;
                        }
                    }
                }
            }
        }

        public bool WaitForConfirms()
        {
            bool timedOut;
            return WaitForConfirms(TimeSpan.FromMilliseconds(Timeout.Infinite), out timedOut);
        }

        public bool WaitForConfirms(TimeSpan timeout)
        {
            bool timedOut;
            return WaitForConfirms(timeout, out timedOut);
        }

        public void WaitForConfirmsOrDie()
        {
            WaitForConfirmsOrDie(TimeSpan.FromMilliseconds(Timeout.Infinite));
        }

        public void WaitForConfirmsOrDie(TimeSpan timeout)
        {
            bool timedOut;
            bool onlyAcksReceived = WaitForConfirms(timeout, out timedOut);
            if (!onlyAcksReceived) {
                Close(new ShutdownEventArgs(ShutdownInitiator.Application,
                                            Constants.ReplySuccess,
                                            "Nacks Received", new IOException("nack received")),
                      false);
                throw new IOException("Nacks Received");
            }
            if (timedOut) {
                Close(new ShutdownEventArgs(ShutdownInitiator.Application,
                                            Constants.ReplySuccess,
                                            "Timed out waiting for acks",
                                            new IOException("timed out waiting for acks")),
                      false);
                throw new IOException("Timed out waiting for acks");
            }
        }

        public abstract void _Private_ConfirmSelect(bool nowait);

        public string BasicConsume(string queue,
                                   bool noAck,
                                   IBasicConsumer consumer)
        {
            return BasicConsume(queue, noAck, "", consumer);
        }

        public string BasicConsume(string queue,
                                   bool noAck,
                                   string consumerTag,
                                   IBasicConsumer consumer)
        {
            return BasicConsume(queue, noAck, consumerTag, null, consumer);
        }

        public string BasicConsume(string queue,
                                   bool noAck,
                                   string consumerTag,
                                   IDictionary<string, object> arguments,
                                   IBasicConsumer consumer)
        {
            return BasicConsume(queue, noAck, consumerTag, false, false, arguments, consumer);
        }

        public class BasicConsumerRpcContinuation : SimpleBlockingRpcContinuation
        {
            public IBasicConsumer m_consumer;
            public string m_consumerTag;
            public BasicConsumerRpcContinuation() { }
        }

        public string BasicConsume(string queue,
                                   bool noAck,
                                   string consumerTag,
                                   bool noLocal,
                                   bool exclusive,
                                   IDictionary<string, object> arguments,
                                   IBasicConsumer consumer)
        {
            ModelShutdown += new ModelShutdownEventHandler(consumer.HandleModelShutdown);

            BasicConsumerRpcContinuation k = new BasicConsumerRpcContinuation();
            k.m_consumer = consumer;

            Enqueue(k);
            // Non-nowait. We have an unconventional means of getting
            // the RPC response, but a response is still expected.
            _Private_BasicConsume(queue, consumerTag, noLocal, noAck, exclusive,
                                  /*nowait:*/ false, arguments);
            k.GetReply();
            string actualConsumerTag = k.m_consumerTag;

            return actualConsumerTag;
        }

        public void HandleBasicConsumeOk(string consumerTag)
        {
            BasicConsumerRpcContinuation k =
                (BasicConsumerRpcContinuation)m_continuationQueue.Next();
            k.m_consumerTag = consumerTag;
            lock (m_consumers)
            {
                m_consumers[consumerTag] = k.m_consumer;
            }
            try {
                k.m_consumer.HandleBasicConsumeOk(consumerTag);
            } catch (Exception e) {
                // FIXME: should we propagate the exception to the
                // caller of BasicConsume?
                CallbackExceptionEventArgs args = new CallbackExceptionEventArgs(e);
                args.Detail["consumer"] = k.m_consumer;
                args.Detail["context"] = "HandleBasicConsumeOk";
                OnCallbackException(args);
            }
            k.HandleCommand(null); // release the continuation.
        }

        public void BasicCancel(string consumerTag)
        {
            BasicConsumerRpcContinuation k = new BasicConsumerRpcContinuation();
            k.m_consumerTag = consumerTag;

            Enqueue(k);

            _Private_BasicCancel(consumerTag, false);
            k.GetReply();

            ModelShutdown -= new ModelShutdownEventHandler(k.m_consumer.HandleModelShutdown);
        }

        public void HandleBasicCancelOk(string consumerTag)
        {
            BasicConsumerRpcContinuation k =
                (BasicConsumerRpcContinuation)m_continuationQueue.Next();

            Trace.Assert(k.m_consumerTag == consumerTag, string.Format(
                                                                       "Consumer tag mismatch during cancel: {0} != {1}",
                                                                       k.m_consumerTag,
                                                                       consumerTag
                                                                       ));

            lock (m_consumers)
            {
                k.m_consumer = (IBasicConsumer)m_consumers[consumerTag];
                m_consumers.Remove(consumerTag);
            }
            try {
                k.m_consumer.HandleBasicCancelOk(consumerTag);
            } catch (Exception e) {
                // FIXME: should we propagate the exception to the
                // caller of BasicCancel?
                CallbackExceptionEventArgs args = new CallbackExceptionEventArgs(e);
                args.Detail["consumer"] = k.m_consumer;
                args.Detail["context"] = "HandleBasicCancelOk";
                OnCallbackException(args);
            }
            k.HandleCommand(null); // release the continuation.
        }

        public class BasicGetRpcContinuation : SimpleBlockingRpcContinuation
        {
            public BasicGetResult m_result;
            public BasicGetRpcContinuation() { }
        }

        public BasicGetResult BasicGet(string queue,
                                       bool noAck)
        {
            BasicGetRpcContinuation k = new BasicGetRpcContinuation();
            Enqueue(k);
            _Private_BasicGet(queue, noAck);
            k.GetReply();
            return k.m_result;
        }

        public abstract void _Private_BasicRecover(bool requeue);

        public void BasicRecover(bool requeue)
        {
            SimpleBlockingRpcContinuation k = new SimpleBlockingRpcContinuation();

            Enqueue(k);
            _Private_BasicRecover(requeue);
            k.GetReply();
        }

        public abstract void BasicQos(uint prefetchSize,
                                      ushort prefetchCount,
                                      bool global);

        public abstract void _Private_BasicConsume(string queue,
                                                   string consumerTag,
                                                   bool noLocal,
                                                   bool noAck,
                                                   bool exclusive,
                                                   bool nowait,
                                                   IDictionary<string, object> arguments);

        public abstract void _Private_BasicCancel(string consumerTag,
                                                  bool nowait);

        public void BasicPublish(PublicationAddress addr,
                                 IBasicProperties basicProperties,
                                 byte[] body)
        {
            BasicPublish(addr.ExchangeName,
                         addr.RoutingKey,
                         basicProperties,
                         body);
        }

        public void BasicPublish(string exchange,
                                 string routingKey,
                                 IBasicProperties basicProperties,
                                 byte[] body)
        {
            BasicPublish(exchange,
                         routingKey,
                         false,
                         basicProperties,
                         body);
        }

        public void BasicPublish(string exchange,
                                 string routingKey,
                                 bool mandatory,
                                 IBasicProperties basicProperties,
                                 byte[] body)
        {
            BasicPublish(exchange,
                         routingKey,
                         mandatory,
                         false,
                         basicProperties,
                         body);
        }

        public void BasicPublish(string exchange,
                                 string routingKey,
                                 bool mandatory,
                                 bool immediate,
                                 IBasicProperties basicProperties,
                                 byte[] body)
        {
            if (basicProperties == null)
            {
                basicProperties = CreateBasicProperties();
            }
            if (m_nextPubSeqNo > 0) {
                lock(m_unconfirmedSet.SyncRoot)
                {
                    if(!m_unconfirmedSet.Contains(m_nextPubSeqNo))
                    {
                        m_unconfirmedSet.Add(m_nextPubSeqNo);
                    }
                    m_nextPubSeqNo++;
                }
            }
            _Private_BasicPublish(exchange,
                                  routingKey,
                                  mandatory,
                                  immediate,
                                  basicProperties,
                                  body);
        }

        public abstract void _Private_BasicPublish(string exchange,
                                                   string routingKey,
                                                   bool mandatory,
                                                   bool immediate,
                                                   IBasicProperties basicProperties,
                                                   byte[] body);

        public abstract void BasicAck(ulong deliveryTag,
                                      bool multiple);

        public abstract void BasicReject(ulong deliveryTag,
                                         bool requeue);

        public abstract void BasicNack(ulong deliveryTag,
                                       bool multiple,
                                       bool requeue);

        public abstract void BasicRecoverAsync(bool requeue);

        public abstract void TxSelect();
        public abstract void TxCommit();
        public abstract void TxRollback();

        void IDisposable.Dispose()
        {
            Close();
        }

        public void Close()
        {
            Close(Constants.ReplySuccess, "Goodbye");
        }

        public void Close(ushort replyCode, string replyText)
        {
            Close(replyCode, replyText, false);
        }

        public void Abort()
        {
            Abort(Constants.ReplySuccess, "Goodbye");
        }

        public void Abort(ushort replyCode, string replyText)
        {
            Close(replyCode, replyText, true);
        }

        public void Close(ushort replyCode, string replyText, bool abort)
        {
            Close(new ShutdownEventArgs(ShutdownInitiator.Application,
                                        replyCode, replyText),
                  abort);
        }

        public void Close(ShutdownEventArgs reason, bool abort)
        {
            ShutdownContinuation k = new ShutdownContinuation();
            ModelShutdown += new ModelShutdownEventHandler(k.OnShutdown);

            try {
                if (SetCloseReason(reason))
                {
                    _Private_ChannelClose(reason.ReplyCode, reason.ReplyText, 0, 0);
                }
                k.Wait();
            } catch (AlreadyClosedException ace) {
                if (!abort)
                    throw ace;
            } catch (IOException ioe) {
                if (!abort)
                    throw ioe;
            }
        }

        public void HandleChannelCloseOk()
        {
            FinishClose();
        }

        public abstract void _Private_ChannelOpen(string outOfBand);

        public abstract void _Private_ChannelClose(ushort replyCode,
                                                   string replyText,
                                                   ushort classId,
                                                   ushort methodId);

        public abstract void _Private_ChannelCloseOk();

        public abstract void _Private_BasicGet(string queue,
                                               bool noAck);

        public void HandleBasicGetOk(ulong deliveryTag,
                                     bool redelivered,
                                     string exchange,
                                     string routingKey,
                                     uint messageCount,
                                     IBasicProperties basicProperties,
                                     byte[] body)
        {
            BasicGetRpcContinuation k = (BasicGetRpcContinuation)m_continuationQueue.Next();
            k.m_result = new BasicGetResult(deliveryTag,
                                            redelivered,
                                            exchange,
                                            routingKey,
                                            messageCount,
                                            basicProperties,
                                            body);
            k.HandleCommand(null); // release the continuation.
        }

        public void HandleBasicGetEmpty()
        {
            BasicGetRpcContinuation k = (BasicGetRpcContinuation)m_continuationQueue.Next();
            k.m_result = null;
            k.HandleCommand(null); // release the continuation.
        }

        public void HandleBasicRecoverOk()
        {
            SimpleBlockingRpcContinuation k = (SimpleBlockingRpcContinuation)m_continuationQueue.Next();
            OnBasicRecoverOk(new EventArgs());
            k.HandleCommand(null);
        }

        public class ConnectionStartRpcContinuation : SimpleBlockingRpcContinuation
        {
            public ConnectionSecureOrTune m_result;
            public ConnectionStartRpcContinuation() { }
        }

        public ConnectionSecureOrTune ConnectionStartOk(IDictionary<string, object> clientProperties,
                                                        string mechanism,
                                                        byte[] response,
                                                        string locale)
        {
            ConnectionStartRpcContinuation k = new ConnectionStartRpcContinuation();
            Enqueue(k);
            try
            {
                _Private_ConnectionStartOk(clientProperties, mechanism,
                                           response, locale);
            }
            catch (AlreadyClosedException)
            {
                // let continuation throw OperationInterruptedException,
                // which is a much more suitable exception before connection
                // negotiation finishes
            }
            k.GetReply();
            return k.m_result;
        }

        public abstract void _Private_ConnectionStartOk(IDictionary<string, object> clientProperties,
                                                        string mechanism,
                                                        byte[] response,
                                                        string locale);

        public void HandleConnectionSecure(byte[] challenge)
        {
            ConnectionStartRpcContinuation k = (ConnectionStartRpcContinuation)m_continuationQueue.Next();
            k.m_result = new ConnectionSecureOrTune();
            k.m_result.m_challenge = challenge;
            k.HandleCommand(null); // release the continuation.
        }

        public ConnectionSecureOrTune ConnectionSecureOk(byte[] response)
        {
            ConnectionStartRpcContinuation k = new ConnectionStartRpcContinuation();
            Enqueue(k);
            try
            {
                _Private_ConnectionSecureOk(response);
            }
            catch (AlreadyClosedException)
            {
                // let continuation throw OperationInterruptedException,
                // which is a much more suitable exception before connection
                // negotiation finishes
            }
            k.GetReply();
            return k.m_result;
        }

        public abstract void _Private_ConnectionSecureOk(byte[] response);

        ///<summary>Handle incoming Connection.Tune
        ///methods.</summary>
        public void HandleConnectionTune(ushort channelMax,
                                         uint frameMax,
                                         ushort heartbeat)
        {
            ConnectionStartRpcContinuation k = (ConnectionStartRpcContinuation)m_continuationQueue.Next();
            k.m_result = new ConnectionSecureOrTune();
            k.m_result.m_tuneDetails.m_channelMax = channelMax;
            k.m_result.m_tuneDetails.m_frameMax = frameMax;
            k.m_result.m_tuneDetails.m_heartbeat = heartbeat;
            k.HandleCommand(null); // release the continuation.
        }

        public abstract void ConnectionTuneOk(ushort channelMax,
                                              uint frameMax,
                                              ushort heartbeat);

        public class ConnectionOpenContinuation : SimpleBlockingRpcContinuation
        {
            public bool m_redirect;
            public string m_host;
            public string m_knownHosts;
            public ConnectionOpenContinuation() { }
        }

        public string ConnectionOpen(string virtualHost,
                                     string capabilities,
                                     bool insist)
        {
            ConnectionOpenContinuation k = new ConnectionOpenContinuation();
            Enqueue(k);
            try {
                _Private_ConnectionOpen(virtualHost, capabilities, insist);
            }
            catch (AlreadyClosedException)
            {
                // let continuation throw OperationInterruptedException,
                // which is a much more suitable exception before connection
                // negotiation finishes
            }
            k.GetReply();
            return k.m_knownHosts;
        }

        public abstract void _Private_ConnectionOpen(string virtualHost,
                                                     string capabilities,
                                                     bool insist);

        public void HandleConnectionOpenOk(string knownHosts)
        {
            ConnectionOpenContinuation k = (ConnectionOpenContinuation)m_continuationQueue.Next();
            k.m_redirect = false;
            k.m_host = null;
            k.m_knownHosts = knownHosts;
            k.HandleCommand(null); // release the continuation.
        }

        public abstract void _Private_ConnectionClose(ushort replyCode,
                                                      string replyText,
                                                      ushort classId,
                                                      ushort methodId);

        public abstract void _Private_ConnectionCloseOk();

        public void HandleQueueDeclareOk(string queue,
                                         uint messageCount,
                                         uint consumerCount)
        {
            QueueDeclareRpcContinuation k = (QueueDeclareRpcContinuation)m_continuationQueue.Next();
            k.m_result = new QueueDeclareOk(queue,
                                            messageCount,
                                            consumerCount);
            k.HandleCommand(null); // release the continuation.
        }

        public override string ToString() {
            return m_session.ToString();
        }
    }
}

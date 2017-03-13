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


namespace RabbitMQ.ServiceModel
{
    using System;
    using System.ServiceModel;
    using System.ServiceModel.Channels;

    internal abstract class RabbitMQOutputChannelBase : RabbitMQChannelBase, IOutputChannel
    {


        private SendOperation m_sendMethod;
        private EndpointAddress m_address;


        protected RabbitMQOutputChannelBase(BindingContext context, EndpointAddress address)
            : base(context)
        {
            m_address = address;
            m_sendMethod = new SendOperation(Send);
        }


        #region Async Methods

        public IAsyncResult BeginSend(Message message, TimeSpan timeout, AsyncCallback callback, object state)
        {
            return m_sendMethod.BeginInvoke(message, timeout, callback, state);
        }

        public IAsyncResult BeginSend(Message message, AsyncCallback callback, object state)
        {
            return m_sendMethod.BeginInvoke(message, Context.Binding.SendTimeout, callback, state);
        }

        public void EndSend(IAsyncResult result)
        {
            m_sendMethod.EndInvoke(result);
        }

        #endregion

        public abstract void Send(Message message, TimeSpan timeout);

        public virtual void Send(Message message)
        {
            Send(message, Context.Binding.SendTimeout);
        }

        public EndpointAddress RemoteAddress
        {
            get { return m_address; }
        }

        public Uri Via
        {
            get { throw new NotImplementedException(); }
        }
    }
}

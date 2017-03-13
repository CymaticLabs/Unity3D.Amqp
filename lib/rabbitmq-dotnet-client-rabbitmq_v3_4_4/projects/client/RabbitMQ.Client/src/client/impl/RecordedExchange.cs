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
using System.Collections.Generic;

using RabbitMQ.Client;
using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Client.Impl
{
    public class RecordedExchange : RecordedNamedEntity
    {
        private bool durable;
        private bool autoDelete;
        private string type;
        private IDictionary<string, object> arguments;

        public RecordedExchange(AutorecoveringModel model, string name) : base(model, name) {}

        public string Type
        {
            get { return type; }
        }

        public bool Durable
        {
            get { return durable; }
        }

        public bool IsAutoDelete
        {
            get { return autoDelete; }
        }

        public IDictionary<String, object> Arguments
        {
            get { return arguments; }
        }

        public RecordedExchange WithDurable(bool value)
        {
            this.durable = value;
            return this;
        }

        public RecordedExchange WithAutoDelete(bool value)
        {
            this.autoDelete = value;
            return this;
        }

        public RecordedExchange WithType(string value)
        {
            this.type = value;
            return this;
        }

        public RecordedExchange WithArguments(IDictionary<string, object> value)
        {
            this.arguments = value;
            return this;
        }

        public void Recover()
        {
            ModelDelegate.ExchangeDeclare(this.name, this.type,
                                          this.durable, this.autoDelete,
                                          this.arguments);
        }

        public override string ToString()
        {
            return String.Format("{0}: name = '{1}', type = '{2}', durable = {3}, autoDelete = {4}, arguments = '{5}'",
                                 this.GetType().Name, name, type, durable, autoDelete, arguments);
        }
    }
}
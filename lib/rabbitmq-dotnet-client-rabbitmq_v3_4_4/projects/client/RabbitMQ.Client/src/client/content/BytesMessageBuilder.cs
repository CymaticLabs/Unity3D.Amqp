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

using RabbitMQ.Client;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Content {
    ///<summary>Constructs AMQP Basic-class messages binary-compatible
    ///with QPid's "BytesMessage" wire encoding.</summary>
    public class BytesMessageBuilder: BasicMessageBuilder, IBytesMessageBuilder {
        ///<summary>MIME type associated with QPid BytesMessages.</summary>
        public readonly static string MimeType = "application/octet-stream";

        ///<summary>Construct an instance for writing. See superclass.</summary>
        public BytesMessageBuilder(IModel model)
            : base(model)
        {}

        ///<summary>Construct an instance for writing. See superclass.</summary>
        public BytesMessageBuilder(IModel model, int initialAccumulatorSize)
            : base(model, initialAccumulatorSize)
        {}

        ///<summary>Override superclass method to answer our characteristic MIME type.</summary>
        public override string GetDefaultContentType() {
            return MimeType;
        }

        ///<summary>Writes an int value into the message body being assembled.</summary>
        public IBytesMessageBuilder WriteInt32(int value) {
            BytesWireFormatting.WriteInt32(Writer, value);
	    return this;
        }

        ///<summary>Writes a short value into the message body being assembled.</summary>
        public IBytesMessageBuilder WriteInt16(short value) {
            BytesWireFormatting.WriteInt16(Writer, value);
	    return this;
        }

        ///<summary>Writes a byte value into the message body being assembled.</summary>
        public IBytesMessageBuilder WriteByte(byte value) {
            BytesWireFormatting.WriteByte(Writer, value);
	    return this;
        }

        ///<summary>Writes a char value into the message body being assembled.</summary>
        public IBytesMessageBuilder WriteChar(char value) {
            BytesWireFormatting.WriteChar(Writer, value);
	    return this;
        }

        ///<summary>Writes a long value into the message body being assembled.</summary>
        public IBytesMessageBuilder WriteInt64(long value) {
            BytesWireFormatting.WriteInt64(Writer, value);
	    return this;
        }

        ///<summary>Writes a float value into the message body being assembled.</summary>
        public IBytesMessageBuilder WriteSingle(float value) {
            BytesWireFormatting.WriteSingle(Writer, value);
	    return this;
        }

        ///<summary>Writes a double value into the message body being assembled.</summary>
        public IBytesMessageBuilder WriteDouble(double value) {
            BytesWireFormatting.WriteDouble(Writer, value);
	    return this;
        }

        ///<summary>Write a section of a byte array into the message
        ///body being assembled.</summary>
        public IBytesMessageBuilder Write(byte[] source, int offset, int count) {
            BytesWireFormatting.Write(Writer, source, offset, count);
	    return this;
        }

        ///<summary>Write a byte array into the message body being
        ///assembled.</summary>
        public IBytesMessageBuilder WriteBytes(byte[] source) {
            BytesWireFormatting.WriteBytes(Writer, source);
	    return this;
        }

        ///<summary>Writes a string value into the message body being assembled.</summary>
        public IBytesMessageBuilder WriteString(string value) {
            BytesWireFormatting.WriteString(Writer, value);
	    return this;
        }
    }
}

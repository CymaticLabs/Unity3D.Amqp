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
using System.Text;
using System.Collections.Generic;

using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Impl
{
    public class MethodArgumentReader
    {
        private NetworkBinaryReader m_reader;
        private int m_bits;
        private int m_bit;

        public MethodArgumentReader(NetworkBinaryReader reader)
        {
            m_reader = reader;
            ClearBits();
        }

        public NetworkBinaryReader BaseReader { get { return m_reader; } }

        private void ClearBits()
        {
            m_bits = 0;
            m_bit = 0x100;
        }

        public byte ReadOctet()
        {
            ClearBits();
            return WireFormatting.ReadOctet(m_reader);
        }

        public string ReadShortstr()
        {
            ClearBits();
            return WireFormatting.ReadShortstr(m_reader);
        }

        public byte[] ReadLongstr()
        {
            ClearBits();
            return WireFormatting.ReadLongstr(m_reader);
        }

        public ushort ReadShort()
        {
            ClearBits();
            return WireFormatting.ReadShort(m_reader);
        }

        public uint ReadLong()
        {
            ClearBits();
            return WireFormatting.ReadLong(m_reader);
        }

        public ulong ReadLonglong()
        {
            ClearBits();
            return WireFormatting.ReadLonglong(m_reader);
        }

        public bool ReadBit()
        {
            if (m_bit > 0x80)
            {
                m_bits = m_reader.ReadByte();
                m_bit = 0x01;
            }

            bool result = (m_bits & m_bit) != 0;
            m_bit = m_bit << 1;
            return result;
        }

        public IDictionary<string, object> ReadTable()
        {
            ClearBits();
            return WireFormatting.ReadTable(m_reader);
        }

        public AmqpTimestamp ReadTimestamp()
        {
            ClearBits();
            return WireFormatting.ReadTimestamp(m_reader);
        }

        // TODO: Consider using NotImplementedException (?)
        // This is a completely bizarre consequence of the way the
        // Message.Transfer method is marked up in the XML spec.
        public byte[] ReadContent()
        {
            throw new NotSupportedException("ReadContent should not be called");
        }
    }
}

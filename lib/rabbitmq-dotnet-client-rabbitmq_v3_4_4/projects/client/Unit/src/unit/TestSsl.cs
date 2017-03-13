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

using NUnit.Framework;
using System;
using System.Net.Security;
using RabbitMQ.Client;

[TestFixture]
public class TestSsl {

    public void SendReceive(ConnectionFactory cf) {
        using (IConnection conn = cf.CreateConnection()) {
            IModel ch = conn.CreateModel();

            ch.ExchangeDeclare("Exchange_TestSslEndPoint", ExchangeType.Direct);
            String qName = ch.QueueDeclare();
            ch.QueueBind(qName, "Exchange_TestSslEndPoint", "Key_TestSslEndpoint", null);

            string message = "Hello C# SSL Client World";
            byte[] msgBytes =  System.Text.Encoding.UTF8.GetBytes(message);
            ch.BasicPublish("Exchange_TestSslEndPoint", "Key_TestSslEndpoint", null, msgBytes);

            bool noAck = false;
            BasicGetResult result = ch.BasicGet(qName, noAck);
            byte[] body = result.Body;
            string resultMessage = System.Text.Encoding.UTF8.GetString(body);

            Assert.AreEqual(message, resultMessage);
        }
    }

    [Test]
    public void TestServerVerifiedIgnoringNameMismatch() {
        string sslDir = Environment.GetEnvironmentVariable("SSL_CERTS_DIR");
        if (null == sslDir) return;

        ConnectionFactory cf = new ConnectionFactory();
        cf.Ssl.ServerName = "*";
        cf.Ssl.AcceptablePolicyErrors = SslPolicyErrors.RemoteCertificateNameMismatch;
        cf.Ssl.Enabled = true;
        SendReceive(cf);
    }

    [Test]
    public void TestServerVerified() {
        string sslDir = Environment.GetEnvironmentVariable("SSL_CERTS_DIR");
        if (null == sslDir) return;

        ConnectionFactory cf = new ConnectionFactory();
        cf.Ssl.ServerName = System.Net.Dns.GetHostName();
        cf.Ssl.Enabled = true;
        SendReceive(cf);
    }

    [Test]
    public void TestClientAndServerVerified() {
        string sslDir = Environment.GetEnvironmentVariable("SSL_CERTS_DIR");
        if (null == sslDir) return;

        ConnectionFactory cf = new ConnectionFactory();
        cf.Ssl.ServerName = System.Net.Dns.GetHostName();
        Assert.IsNotNull(sslDir);
        cf.Ssl.CertPath = sslDir + "/client/keycert.p12";
        string p12Password = Environment.GetEnvironmentVariable("PASSWORD");
        Assert.IsNotNull(p12Password, "missing PASSWORD env var");
        cf.Ssl.CertPassphrase = p12Password;
        cf.Ssl.Enabled = true;
        SendReceive(cf);
    }
}

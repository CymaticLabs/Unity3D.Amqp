using System;
using System.Linq;

namespace CymaticLabs.Unity3D.Amqp.Cli
{
    class Program
    {
        #region Fields

        // Setup default argument values
        static string server = "localhost";
        static int port = AmqpHelper.DefaultUnsecureAmqpPort;
        static int api = AmqpHelper.DefaultUnsecureWebPort;
        static string virtualHost = AmqpHelper.DefaultVirtualHost;
        static string username = "";
        static string password = "";
        static short reconnectInterval = AmqpHelper.DefaultReconnectInterval;
        static ushort requestedHeartbeat = AmqpHelper.DefaultHeartBeat;
        static string queueName = "";
        static string exchangeName = AmqpHelper.DefaultExchangeName;
        static string routingKey = "";
        static AmqpExchangeTypes exchangeType = AmqpHelper.DefaultExchangeType;
        static string command = "tx";
        static byte count = 10;

        // Used for connection
        static IAmqpBrokerConnection client;
        static AmqpExchangeSubscription rxExSub;
        static AmqpQueueSubscription rxQueueSub;

        static string[] commands = new string[] { "rx", "tx", "exchanges", "queues" };

        #endregion Fields

        static void Main(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    PrintUsage(args);
                    return;
                }

                #region Prepare Arguments

                // Parse arguments and assign values
                foreach (var arg in args)
                {
                    if (!arg.Contains(":"))
                    {
                        PrintUsage(args);
                        return;
                    }

                    var parsedArg = arg.SplitClean(':');
                    var name = parsedArg[0];
                    var value = parsedArg[1];

                    switch (name)
                    {
                        case "server":
                            server = value;
                            break;

                        case "port":
                            port = int.Parse(value);
                            break;

                        case "api":
                            api = int.Parse(value);
                            break;

                        case "vhost":
                            virtualHost = value;
                            break;

                        case "u":
                        case "user":
                        case "username":
                            username = value;
                            break;

                        case "p":
                        case "pass":
                        case "password":
                            password = value;
                            break;

                        case "reconnect":
                            reconnectInterval = short.Parse(value);
                            break;

                        case "heartbeat":
                            requestedHeartbeat = ushort.Parse(value);
                            break;

                        case "cmd":
                        case "command":
                            command = value;
                            break;

                        case "exchange":
                            exchangeName = value;
                            break;

                        case "type":
                            exchangeType = (AmqpExchangeTypes)Enum.Parse(typeof(AmqpExchangeTypes), value, true);
                            break;

                        case "queue":
                            queueName = value;
                            break;

                        case "key":
                        case "routing":
                        case "routingkey":
                            routingKey = value;
                            break;

                        case "count":
                            count = byte.Parse(value);
                            break;
                    }
                }

                #endregion Prepare Arguments

                #region Validate Arguments

                if (!commands.Contains(command))
                {
                    PrintUsage(args);
                    return;
                }

                #endregion Validate Arguments

                // Create a new client using the supplied arguments
                client = AmqpConnectionFactory.Create(server, port, api, virtualHost, username, password, reconnectInterval, requestedHeartbeat);

                // Hook up event handlers
                client.Blocked += Client_Blocked;
                client.Connected += Client_Connected;
                client.Disconnected += Client_Disconnected;
                client.ConnectionAborted += Client_ConnectionAborted;

                // If this is an AMQP operation, connect
                if (command == "rx" || command == "tx")
                {
                    Console.WriteLine("Connecting to: {0}", AmqpHelper.GetConnectionInfo(client));
                    Console.WriteLine("Press enter to exit");
                    client.Connect();
                    Console.ReadLine();
                    client.Disconnect();
                }
                // Otherwise this is a REST API operation so execute it
                else
                {
                    if (command == "exchanges")
                    {
                        Console.WriteLine("Listing exchanges for {0}:{1} and virtual host '{2}'", server, api, virtualHost);

                        foreach (var exchange in client.GetExchanges(virtualHost))
                        {
                            Console.WriteLine("name:{0}, type:{1}, auto delete:{2}, durable:{3}", exchange.Name, exchange.Type.ToString().ToLower(), exchange.AutoDelete, exchange.Durable);
                        }
                    }
                    else if (command == "queues")
                    {
                        Console.WriteLine("Listing queues for {0}:{1} and virtual host '{2}'", server, api, virtualHost);

                        foreach (var queue in client.GetQueues(virtualHost))
                        {
                            Console.WriteLine("name:{0}, auto delete:{1}, durable:{2}, state:{3}, policy:{4}, exclusive:{5}", queue.Name, queue.AutoDelete, queue.Durable, queue.State, queue.Policy, queue.ExclusiveConsumerTag);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0}", ex);
                //Console.ReadLine();
                Environment.Exit(-1);
            }
        }

        #region Connection Handlers

        private static void Client_Disconnected(object sender, EventArgs e)
        {
            Console.WriteLine("Client disconnected!");
        }

        private static void Client_Connected(object sender, EventArgs e)
        {
            Console.WriteLine("Client connected!");

            if (command == "rx")
            {
                Console.WriteLine("Entering receive mode...");

                if (string.IsNullOrEmpty(queueName))
                {
                    // Subscribe to supplied exchange to listen for messages
                    rxExSub = new AmqpExchangeSubscription(exchangeName, exchangeType, routingKey, OnAmqpExchangeMessageReceived);
                    var ex = client.Subscribe(rxExSub);

                    if (ex == null)
                    {
                        Console.WriteLine("Listening for messages on exchange: {0}", exchangeName);
                    }
                    else
                    {
                        Console.WriteLine("Error listening for messages");
                    }
                }
                else
                {
                    rxQueueSub = new AmqpQueueSubscription(queueName, false, OnAmqpQueueMessageReceived);
                    client.Subscribe(rxQueueSub);
                    Console.WriteLine("Listening for messages on queue: {0}", queueName);
                }
            }
            else if (command == "tx")
            {
                Console.WriteLine("Entering send mode...");

                for (var i = 1; i <= count; i++)
                {
                    var payload = i.ToString();
                    Console.WriteLine("[tx] {0} {1}:{2} => {3}", DateTime.Now, exchangeName, routingKey, payload);
                    client.Publish(exchangeName, routingKey, payload);
                    System.Threading.Thread.Sleep(1000); // one per second
                }

                // Disconnect
                client.Disconnect();
            }
        }

        private static void Client_ConnectionAborted(object sender, EventArgs e)
        {
            Console.WriteLine("Client connection aborted!");
        }

        private static void Client_Blocked(object sender, EventArgs e)
        {
            Console.WriteLine("Client blocked!");
        }

        static void OnAmqpExchangeMessageReceived(AmqpExchangeReceivedMessage received)
        {
            // Decode the body as UTF8 text
            var payload = System.Text.Encoding.UTF8.GetString(received.Message.Body);
            Console.WriteLine("[rx] {0} {1}:{2} => {3}", DateTime.Now, exchangeName, routingKey, payload);
        }

        static void OnAmqpQueueMessageReceived(AmqpQueueReceivedMessage received)
        {
            // Decode the body as UTF8 text
            var payload = System.Text.Encoding.UTF8.GetString(received.Message.Body);
            Console.WriteLine("[rx] {0} {1}:{2} => {3}", DateTime.Now, queueName, routingKey, payload);
        }

        #endregion Connection Handlers

        #region Utility

        // Prints the use to the console
        static void PrintUsage(string[] args)
        {
            Console.WriteLine("usage: AmqpCli.exe {options}\n");
            Console.WriteLine("options:\n");
            Console.WriteLine("  server:     the AMQP host address\n");
            Console.WriteLine("  port:       the AMQP port to use (default {0})\n", AmqpHelper.DefaultUnsecureAmqpPort);
            Console.WriteLine("  api:        the web/REST API port to use (default {0})\n", AmqpHelper.DefaultUnsecureWebPort);
            Console.WriteLine("  vhost:      the AMQP virtual host to use (default {0})\n", AmqpHelper.DefaultVirtualHost);
            Console.WriteLine("  u:");
            Console.WriteLine("  user:");
            Console.WriteLine("  username:   the AMQP client username to use\n");
            Console.WriteLine("  p:");
            Console.WriteLine("  pass:");
            Console.WriteLine("  password:   the AMQP client password to use\n");
            Console.WriteLine("  reconnect:  the reconnect/retry interval in seconds (default {0})\n", AmqpHelper.DefaultReconnectInterval);
            Console.WriteLine("  heartbeat:  the connection heartbeat in seconds (default {0})\n", AmqpHelper.DefaultHeartBeat);
            Console.WriteLine("  cmd:");
            Console.WriteLine("  command:    the command to use for testing:");
            Console.WriteLine("              rx: requires an exchange name and exchange type to subscribe to (routing key optional)");
            Console.WriteLine("              tx: requires an exchange name to publish to (routing key optional)");
            Console.WriteLine("              exchanges: requires a virtual host to list exchanges for");
            Console.WriteLine("              queues: requires a virtual host to list queues for\n");
            Console.WriteLine("  exchange:   the name of the exchange to target (default {0})\n", AmqpHelper.DefaultExchangeName);
            Console.WriteLine("  type:       the type of the exchange [topic, fanout, direct, header] (default {0})", AmqpHelper.DefaultExchangeType.ToString().ToLower());
            Console.WriteLine("              [required when subscribing with 'rx']\n");
            Console.WriteLine("  queue:      when using the 'rx' command, if the 'queue' option is supplied instead of 'exchange'");
            Console.WriteLine("              then the subscription will be directly to the named queue instead of an exchange\n");
            Console.WriteLine("  key:");
            Console.WriteLine("  routing:");
            Console.WriteLine("  routingkey: the optional AMQP routing key to use\n");
            Console.WriteLine("  count:      when using the 'tx' command, the number of test messages to send (default 10)");
            Console.WriteLine("              messages are sent as text; once per second\n");
            Console.WriteLine("subscribe example:\n");
            Console.WriteLine("  AmqpCli.exe cmd:rx server:localhost port:5672 u:myuser p:mypassword exchange:myexchange type:topic\n");
            Console.WriteLine("  this will subscribe to the 'myexchange' topic exchange and print received messages\n");
            Console.WriteLine("publish example:\n");
            Console.WriteLine("  AmqpCli.exe cmd:tx server:localhost port:5672 u:myuser p:mypassword exchange:myexchange count:5\n");
            Console.WriteLine("  this will publish 5 messages to the 'myexchange' topic\n");
        }

        #endregion Utility
    }
}

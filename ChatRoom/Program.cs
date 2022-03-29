using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ChatRoom
{
    internal static class Program
    {
        private static int _clientId;
        
        private static void ClientServer(object clientObj)
        {
            var client = (TcpClient) clientObj;
            var localId = _clientId;
            _clientId++;
            var isFromFinished = false;
            var joinMessage = "<0> client " + localId + " has joined the chat."; 
            MessageQueue.Enqueue(joinMessage, localId);
            
            using (var writer = new StreamWriter(client.GetStream()))
            {
                writer.WriteLineAsync("Welcome to the Chat Room!");
            }

            var fromClient = Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    using (var reader = new StreamReader(client.GetStream()))
                    {
                        var message = await reader.ReadLineAsync();
                        if (message is null)
                        {
                            isFromFinished = true;
                            break;
                        }
                        MessageQueue.Enqueue(message, localId);
                    }
                }
            });
            
            var toClient = Task.Factory.StartNew(async () =>
            {
                long ts = 0;
                while (true) {
                    using (var writer = new StreamWriter(client.GetStream()) ) 
                    {
                        var message = MessageQueue.Dequeue(ref ts, localId, isFromFinished);
                        if (!isFromFinished)
                        {
                            await writer.WriteLineAsync("Lol!");
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            });

            try
            {
                Task.WaitAll(fromClient, toClient);  // Runs both tasks simultaneously
            }
            catch (AggregateException ex)
            {
                Console.WriteLine(ex.Flatten().Message);
            }
            
            var leaveMessage = "<0> client " + localId + " has left the chat."; 
            MessageQueue.Enqueue(leaveMessage, localId);
            
            client.Close();
        }
        
        private static void Main(string[] args)
        {
            if (args.Length > 1)
            {
                Console.WriteLine("Error - only 1 optional argument expected - '-show'.");
            }
            else
            {
                if (args.Length == 1)
                {
                    if (args[0] == "-show")
                    {
                        MessageQueue.Show = true;
                    }
                    else
                    {
                        Console.WriteLine("Error - unknown argument " + args[0]);
                        return;
                    }
                }

                var server = new TcpListener(IPAddress.Any, 3003);
                server.Start();
                while (true)
                {
                    var client = server.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(ClientServer, client);
                }
            }
        }
    }
}
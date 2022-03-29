using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ChatRoom
{
    internal static class Program
    {
        private static void ClientServer(object clientObj)
        {
            var client = (TcpClient) clientObj;
            var tasks = new List<Task>();
            var isFromFinished = false;

            using (var writer = new StreamWriter(client.GetStream()))
            {
                writer.WriteLineAsync("Welcome to the Chat Room!");
            }
            
            async Task FromClient()
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
                        MessageQueue.Enqueue(message, (int) MessageQueue.Now);
                    }
                }
            }
            tasks.Add(FromClient());
            
            async Task ToClient()
            {
                long ts = 0;
                while (true) {
                    await using (var writer = new StreamWriter(client.GetStream()))
                    {
                        var message = MessageQueue.Dequeue(ref ts, 0, isFromFinished);
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
            }
            tasks.Add(ToClient());
            
            var waitTask = Task.WhenAll(tasks);
            waitTask.Wait();
            client.Close();
        }
        
        private static void Main(string[] args)
        {
            if (args.Length > 1)
            {
                Console.WriteLine("Error - only 1 optional argument expected - 'show'.");
            }
            else
            {
                if (args.Length == 1)
                {
                    if (args[0] == "show")
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
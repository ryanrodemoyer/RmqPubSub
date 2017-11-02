using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading;
using CommandLine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace RmqPubSub
{
    class Program
    {
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Consumer, Publish, Default>(args)
                .WithParsed<Default>(options =>
                {
                    if (options.Debug)
                    {
                        Debugger.Launch();
                    }

                    var worker = new BackgroundWorker { WorkerSupportsCancellation = true, WorkerReportsProgress = true };

                    worker.DoWork += Worker_DoWork;
                    worker.ProgressChanged += Worker_ProgressChanged;

                    worker.RunWorkerAsync(new Consumer { Server = options.Server, Queue = options.Queue, Username = options.Username, Password = options.Password, VirtualHost = options.VirtualHost });

                    ConnectionFactory factory = new ConnectionFactory
                    {
                        HostName = options.Server,
                        UserName = options.Username,
                        Password = options.Password,
                        VirtualHost = options.VirtualHost
                    };

                    for (int i = 1; i <= 5; i++)
                    {
                        using (var connection = factory.CreateConnection())
                        {
                            using (var channel = connection.CreateModel())
                            {
                                var obj = new { Count = i, Message = "my message" };
                                var data = JsonConvert.SerializeObject(obj);
                                byte[] body = Encoding.UTF8.GetBytes(data);

                                channel.BasicPublish(exchange: options.Exchange, routingKey: options.Queue, basicProperties: null, body: body);

                                Console.WriteLine($"Message #{i} sent. Data={data}");
                            }
                        }
                    }

                    Console.WriteLine("Press q to quit.");
                    while (true)
                    {
                        string input = Console.ReadLine();
                        if (input == "q")
                        {
                            worker.CancelAsync();

                            Thread.Sleep(2000); // pause so we allow the updates from the backgroundworker to flow to the UI

                            break;
                        }
                    }
                })
                .WithParsed<Consumer>(options =>
                {
                    if (options.Debug)
                    {
                        Debugger.Launch();
                    }

                    var workers = new List<BackgroundWorker>(options.HowMany);

                    for (int i = 0; i < options.HowMany; i++)
                    {
                        var worker = new BackgroundWorker { WorkerSupportsCancellation = true, WorkerReportsProgress = true };

                        worker.DoWork += Worker_DoWork;
                        worker.ProgressChanged += Worker_ProgressChanged;

                        var context = new ConsumerContext
                        {
                            ConsumerId = i,
                            Server = options.Server,
                            VirtualHost = options.VirtualHost,
                            Username = options.Username,
                            Password = options.Password,
                            Queue = options.Queue
                        };

                        worker.RunWorkerAsync(context);

                        workers.Add(worker);
                    }
                    
                    Console.WriteLine("Press q to quit.");
                    while (true)
                    {
                        string input = Console.ReadLine();
                        if (input == "q")
                        {
                            foreach (var worker in workers)
                            {
                                worker.CancelAsync();
                            }

                            Thread.Sleep(2000); // pause so we allow the updates from the backgroundworker to flow to the UI

                            break;
                        }
                    }
                })
                .WithParsed<Publish>(options =>
                {
                    if (options.Debug)
                    {
                        Debugger.Launch();
                    }

                    ConnectionFactory factory = new ConnectionFactory
                    {
                        HostName = options.Server,
                        UserName = options.Username,
                        Password = options.Password,
                        VirtualHost = options.VirtualHost
                    };

                    for (int i = 1; i <= options.Times; i++)
                    {
                        using (var connection = factory.CreateConnection())
                        {
                            using (var channel = connection.CreateModel())
                            {
                                var obj = new { Count = i, Message = options.Message };
                                var data = JsonConvert.SerializeObject(obj);
                                byte[] body = Encoding.UTF8.GetBytes(data);

                                channel.BasicPublish(exchange: options.Exchange, routingKey: options.Queue, basicProperties: null, body: body);

                                Console.WriteLine($"Message #{i} sent. Data={data}");
                            }
                        }
                    }
                })
                ;
        }

        private static void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            string message = (string)e.UserState;
            Console.WriteLine(message);
        }

        private static void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            var worker = (BackgroundWorker)sender;
            var options = (ConsumerContext)e.Argument;

            ConnectionFactory factory = new ConnectionFactory
            {
                HostName = options.Server,
                UserName = options.Username,
                Password = options.Password,
                VirtualHost = options.VirtualHost
            };

            IConnection connection = factory.CreateConnection();
            IModel channel = connection.CreateModel();

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                var message = Encoding.UTF8.GetString(ea.Body);
                JObject obj = JObject.Parse(message);
                int number = Convert.ToInt32(obj["Count"]);
                worker.ReportProgress(0, $"ConsumerId={options.ConsumerId}. Received message #{number}");

                try
                {
                    // ReSharper disable once AccessToDisposedClosure
                    channel.BasicAck(ea.DeliveryTag, multiple: false);
                }
                catch (AlreadyClosedException ex)
                {
                    worker.ReportProgress(0, ex.Message);
                }
            };
            consumer.Shutdown += (s, sea) =>
            {
                worker.ReportProgress(0, $"ConsumerId={options.ConsumerId}.consumer shutdown requested. data={sea.ReplyText}");
            };

            channel.BasicConsume(queue: options.Queue, noAck: false, consumer: consumer);

            worker.ReportProgress(0, $"ConsumerId={options.ConsumerId}. Consumer is listening.");

            while (!worker.CancellationPending) { continue; } // hang out here to let the consumers do their thing

            worker.ReportProgress(0, $"ConsumerId={options.ConsumerId}. Consumer closing...");

            channel.Dispose();
            connection.Dispose();

            worker.ReportProgress(0, $"ConsumerId={options.ConsumerId}. Consumer closed.");
        }
    }

    public class ConsumerContext
    {
        public string Server { get; set; }
        public string VirtualHost { get; set; }
        public string Queue { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public int ConsumerId { get; set; }
    }

    [Verb("consume", HelpText="Create one or more long-running consumer(s) that listens for messages.")]
    public class Consumer
    {
        [Option("debug", HelpText = "Launch a debugger as the first action.")]
        public bool Debug { get; set; }

        [Option("server", Required = true, HelpText = "DNS name or IP address of the cluster or individual node.")]
        public string Server { get; set; }

        [Option("vhost", Required = true, HelpText = "Name of the RabbitMQ virtual host.")]
        public string VirtualHost { get; set; }

        [Option("queue", Required = true, HelpText = "Name of the RabbitMQ queue.")]
        public string Queue { get; set; }

        [Option("username", Required = true, HelpText = "Username for the RabbitMQ user.")]
        public string Username { get; set; }

        [Option("password", Required = true, HelpText = "Password for the RabbitMQ user.")]
        public string Password { get; set; }

        [Option("howmany", Default = 1, HelpText = "Number of consumers to create.")]
        public int HowMany { get; set; }
    }

    [Verb("publish", HelpText = "Publish one or more messages to the RabbitMQ cluster.")]
    public class Publish
    {
        [Option("debug", HelpText = "Launch a debugger as the first action.")]
        public bool Debug { get; set; }

        [Option("server", Required=true, HelpText = "DNS name or IP address of the cluster or individual node.")]
        public string Server { get; set; }

        [Option("vhost", Required = true, HelpText = "Name of the RabbitMQ virtual host.")]
        public string VirtualHost { get; set; }

        [Option("exchange", Required = true, HelpText = "Name of the RabbitMQ exchange.")]
        public string Exchange { get; set; }

        [Option("queue", Required = true, HelpText = "Name of the RabbitMQ queue.")]
        public string Queue { get; set; }

        [Option("username", Required = true, HelpText = "Username for the RabbitMQ user.")]
        public string Username { get; set; }

        [Option("password", Required = true, HelpText = "Password for the RabbitMQ user.")]
        public string Password { get; set; }

        [Option("message", Required = true, HelpText = "Data to include with the message.")]
        public string Message { get; set; }
        
        [Option("times", Default = 1, HelpText = "Number of messages to send.")]
        public int Times { get; set; }
    }

    [Verb("default")]
    public class Default
    {
        [Option("debug", HelpText = "Launch a debugger as the first action.")]
        public bool Debug { get; set; }

        [Option("server", Required = true, HelpText = "DNS name or IP address of the cluster or individual node.")]
        public string Server { get; set; }

        [Option("vhost", Required = true, HelpText = "Name of the RabbitMQ virtual host.")]
        public string VirtualHost { get; set; }

        [Option("exchange", Required = true, HelpText = "Name of the RabbitMQ exchange.")]
        public string Exchange { get; set; }

        [Option("queue", Required = true, HelpText = "Name of the RabbitMQ queue.")]
        public string Queue { get; set; }

        [Option("username", Required = true, HelpText = "Username for the RabbitMQ user.")]
        public string Username { get; set; }

        [Option("password", Required = true, HelpText = "Password for the RabbitMQ user.")]
        public string Password { get; set; }
    }

}
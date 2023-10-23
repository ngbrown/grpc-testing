using RabbitMQ.Client;
using RabbitMqGreeterClient.Client;

namespace RabbitMqGreeterClient
{
    internal class Program
    {
        private const string QUEUE_NAME = "rpc_queue";

        static async Task<int> Main(string[] args)
        {
            CancellationTokenSource consoleCts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                Console.WriteLine("Cancel event triggered");
                consoleCts.Cancel();
                eventArgs.Cancel = true;
            };

            Console.WriteLine("RPC Client");
            string argN = args.Length > 0 ? args[0] : "46";
            var n = int.Parse(argN);
            if (n > 46)
            {
                Console.WriteLine("Argument exceeds possible bounds. Limit to 46 or less.");
                return 1;
            }

            try
            {
                await InvokeAsync(n, consoleCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex)
            {
                if (!consoleCts.IsCancellationRequested) throw;
            }

            return 0;
        }

        private static async Task InvokeAsync(int max, CancellationToken cancellationToken)
        {
            var factory = new ConnectionFactory { HostName = "localhost", UserName = "guest", Password = "guest"  };
            using var rabbitChannel = RabbitRpcChannel.ForQueue(factory, QUEUE_NAME);
            var greeterClient = new Greeter.GreeterClient(rabbitChannel);
            var fibClient = new Fibonacci.FibonacciClient(rabbitChannel);

            var helloReply = await greeterClient.SayHelloAsync(
                new HelloRequest { Name = $"RabbitMQ GreeterClient" }, cancellationToken: cancellationToken);
            Console.WriteLine("Greeting: " + helloReply.Message);

            var rng = new Random();
            var timeout = TimeSpan.FromSeconds(1);
            for (;;)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var next = NextIntBetween(rng, 2, max);
                Console.WriteLine(" [x] Requesting fib({0})", next);
                try
                {
                    var fibReply = await fibClient.FibAsync(new FibonacciRequest { Max = next },
                        deadline: DateTime.UtcNow.Add(timeout), cancellationToken: cancellationToken);
                    Console.WriteLine(" [.] Got '{0}'", fibReply.Number);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            }
        }

        private static int NextIntBetween(Random rng, int min, int max)
        {
            return min + (int)Math.Round(rng.NextDouble() * (max - min));
        }
    }
}
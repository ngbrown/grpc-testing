using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using RabbitMQ.Client;
using System.Globalization;

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

        [SuppressMessage("ReSharper", "FunctionNeverReturns")]
        private static async Task InvokeAsync(int max, CancellationToken cancellationToken)
        {
            var timeout = TimeSpan.FromMilliseconds(1000);
            var factory = new ConnectionFactory { HostName = "localhost", UserName = "guest", Password = "guest"  };

            using var rpcClient = RabbitRpcClient.Connect(factory, QUEUE_NAME);
            rpcClient.Timeout = timeout;
            var rng = new Random();

            for (;;)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var next = NextIntBetween(rng, 2, max);
                Console.WriteLine(" [x] Requesting fib({0})", next);

                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    var response = await rpcClient.CallAsync(next.ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
                    stopwatch.Stop();
                    Console.WriteLine(" [.] Got '{0}' in {1:N2} ms", response, stopwatch.Elapsed.TotalMilliseconds);
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
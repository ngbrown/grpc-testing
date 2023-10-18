using System.Globalization;
using System.Security.Cryptography;

namespace RabbitMqGreeterClient
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("RPC Client");
            string argN = args.Length > 0 ? args[0] : "46";
            var n = int.Parse(argN);
            if (n > 46)
            {
                Console.WriteLine("Argument exceeds possible bounds. Limit to 46 or less.");
                return 1;
            }
            await InvokeAsync(n).ConfigureAwait(false);

            Console.WriteLine(" Press [enter] to exit.");
            Console.ReadLine();
            return 0;
        }

        private static async Task InvokeAsync(int max)
        {
            using var rpcClient = RpcClient.Connect();
            rpcClient.Timeout = TimeSpan.FromMilliseconds(1000);
            var rng = new Random();

            for (;;)
            {
                var next = NextIntBetween(rng, 2, max);
                Console.WriteLine(" [x] Requesting fib({0})", next);

                try
                {
                    var response = await rpcClient.CallAsync(next.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
                    Console.WriteLine(" [.] Got '{0}'", response);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }
        }

        private static int NextIntBetween(Random rng, int min, int max)
        {
            return min + (int)Math.Round(rng.NextDouble() * (max - min));
        }
    }
}
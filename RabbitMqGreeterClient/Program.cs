using System.Globalization;
using System.Security.Cryptography;

namespace RabbitMqGreeterClient
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("RPC Client");
            string n = args.Length > 0 ? args[0] : "43";
            await InvokeAsync(int.Parse(n)).ConfigureAwait(false);

            Console.WriteLine(" Press [enter] to exit.");
            Console.ReadLine();
        }

        private static async Task InvokeAsync(int max)
        {
            using var rpcClient = RpcClient.Connect();
            rpcClient.Timeout = TimeSpan.FromMilliseconds(1000);
            var rng = new Random();

            for (;;)
            {
                var next = (int)Math.Round(rng.NextDouble() * max);
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

                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
    }
}
namespace RabbitMqGreeterClient
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("RPC Client");
            string n = args.Length > 0 ? args[0] : "30";
            await InvokeAsync(n).ConfigureAwait(false);

            Console.WriteLine(" Press [enter] to exit.");
            Console.ReadLine();
        }

        private static async Task InvokeAsync(string n)
        {
            using var rpcClient = new RpcClient();

            Console.WriteLine(" [x] Requesting fib({0})", n);
            var response = await rpcClient.CallAsync(n).ConfigureAwait(false);
            Console.WriteLine(" [.] Got '{0}'", response);
        }
    }
}
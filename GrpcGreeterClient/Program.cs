using Grpc.Net.Client;

namespace GrpcGreeterClient
{
    internal class Program
    {
        private static async Task<int> Main(string[] args)
        {
            CancellationTokenSource consoleCts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                Console.WriteLine("Cancel event triggered");
                consoleCts.Cancel();
                eventArgs.Cancel = true;
            };

            try
            {
                // The port number must match the port of the gRPC server.
                using var channel = GrpcChannel.ForAddress("https://localhost:7028");
                var client = new Greeter.GreeterClient(channel);
                var reply = await client.SayHelloAsync(
                    new HelloRequest { Name = "GreeterClient" }, cancellationToken: consoleCts.Token);
                Console.WriteLine("Greeting: " + reply.Message);
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
            catch (OperationCanceledException ex)
            {
                if (!consoleCts.IsCancellationRequested) throw;
            }

            return 0;
        }
    }
}
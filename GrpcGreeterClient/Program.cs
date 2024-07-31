using Grpc.Core;
using Grpc.Net.Client;

namespace GrpcGreeterClient
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            // The port number must match the port of the gRPC server.
            //using var channel = GrpcChannel.ForAddress("https://localhost:7028");
            using var channel = GrpcChannel.ForAddress("http://localhost:5274");

            var client = new Greeter.GreeterClient(channel);
            var reply = await client.SayHelloAsync(
                new HelloRequest { Name = "GreeterClient" });
            Console.WriteLine("Greeting: " + reply.Message);

            // expected to fail
            try
            {
                await client.SayHelloAsync(
                    new HelloRequest { Name = "nobody" });
            }
            catch (RpcException ex)
            {
                Console.WriteLine(ex);
                if (ex.StatusCode != StatusCode.PermissionDenied)
                {
                    throw;
                }
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
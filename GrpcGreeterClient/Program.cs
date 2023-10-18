﻿using Grpc.Net.Client;

namespace GrpcGreeterClient
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            // The port number must match the port of the gRPC server.
            using var channel = GrpcChannel.ForAddress("https://localhost:7028");
            var client = new Greeter.GreeterClient(channel);
            var reply = await client.SayHelloAsync(
                new HelloRequest { Name = "GreeterClient" });
            Console.WriteLine("Greeting: " + reply.Message);
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
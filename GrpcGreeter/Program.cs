using System.IO.Compression;
using System.Net;
using GrpcGreeter.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace GrpcGreeter
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            //builder.WebHost.ConfigureKestrel(cfg =>
            //{
            //    var ipEndPoint = new IPEndPoint(IPAddress.Any, 14072);
            //    cfg.Listen(ipEndPoint, listenOptions =>
            //    {
            //        listenOptions.Protocols = HttpProtocols.Http2;
            //    });
            //});

            // Additional configuration is required to successfully run gRPC on macOS.
            // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682

            // Add services to the container.
            builder.Services.AddGrpc(cfg =>
            {
                cfg.ResponseCompressionLevel = CompressionLevel.Optimal;
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            app.MapGrpcService<GreeterService>();
            app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

            app.Run();
        }
    }
}
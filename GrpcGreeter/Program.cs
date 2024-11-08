using GrpcGreeter.Services;
using Serilog;
using Serilog.Templates.Themes;
using SerilogTracing;
using SerilogTracing.Expressions;

namespace GrpcGreeter
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .Enrich.WithProperty("Application", typeof(Program).Assembly.GetName().Name)
                .WriteTo.Console(Formatters.CreateConsoleTextFormatter(TemplateTheme.Code))
                .WriteTo.Seq("http://localhost:5341")
                .CreateLogger();

            try
            {
                var builder = WebApplication.CreateBuilder(args);

                // Additional configuration is required to successfully run gRPC on macOS.
                // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682

                // Add services to the container.
                builder.Services.AddSerilog();
                builder.Services.AddSingleton(
                    new ActivityListenerConfiguration()
                        .Instrument.AspNetCoreRequests()
                        .Instrument.RabbitMQClient()
                        .TraceToSharedLogger());
                builder.Services.AddGrpc();

                builder.Services.AddSingleton<FibService>();
                builder.Services.AddHostedService<RabbitMqServer>();

                var app = builder.Build();

                // Configure the HTTP request pipeline.
                app.MapGrpcService<GreeterService>();
                app.MapGet("/",
                    () =>
                        "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
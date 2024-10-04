using System.Diagnostics;
using GrpcGreeter.Services;
using Serilog;
using Serilog.Events;
using Serilog.Parsing;
using Serilog.Templates.Themes;
using SerilogTracing;
using SerilogTracing.Expressions;
using SerilogTracing.Instrumentation;

namespace GrpcGreeter
{
    public class Program
    {
        // Works in the console, but Seq (as of 2024.3.12250) doesn't yet seem to support this
        const bool UseDottedPropertyNames = false;

        public static void Main(string[] args)
        {
            AppContext.SetSwitch("Serilog.Parsing.MessageTemplateParser.AcceptDottedPropertyNames", UseDottedPropertyNames);

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

                AddAmqpActivityInterceptor();

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

        private static void AddAmqpActivityInterceptor()
        {
            var messageTemplateSubscriber =
                new MessageTemplateParser().Parse(UseDottedPropertyNames
                    ? "AMQP {messaging.operation.type} {messaging.rabbitmq.destination.routing_key}"
                    : "AMQP {OperationType} {RoutingKey}");
            var messageTemplatePublisher =
                new MessageTemplateParser().Parse(UseDottedPropertyNames
                    ? "AMQP {messaging.operation.type} {messaging.destination.name}"
                    : "AMQP {OperationType} {DestinationName}");

            var activityListener = new ActivityListener
            {
                ShouldListenTo = source =>
                    source.Name == "RabbitMQ.Client.Subscriber" ||
                    source.Name == "RabbitMQ.Client.Publisher",
                ActivityStopped = OnActivityStopped
            };
            ActivitySource.AddActivityListener(activityListener);
            return;

            void OnActivityStopped(Activity activity)
            {
                if (activity.Source.Name == "RabbitMQ.Client.Subscriber")
                {
                    ActivityInstrumentation.SetMessageTemplateOverride(activity, messageTemplateSubscriber);
                    foreach (var (k, v) in activity.Tags)
                    {
                        switch (k)
                        {
                            case "messaging.operation.type":
                                ActivityInstrumentation.SetLogEventProperty(
                                    activity,
                                    UseDottedPropertyNames ? k : "OperationType",
                                    new ScalarValue(v));
                                break;
                            case "messaging.rabbitmq.destination.routing_key":
                                ActivityInstrumentation.SetLogEventProperty(
                                    activity,
                                    UseDottedPropertyNames ? k : "RoutingKey",
                                    new ScalarValue(v));
                                break;
                        }
                    }
                }
                else if (activity.Source.Name == "RabbitMQ.Client.Publisher")
                {
                    ActivityInstrumentation.SetMessageTemplateOverride(activity, messageTemplatePublisher);
                    foreach (var (k, v) in activity.Tags)
                    {
                        switch (k)
                        {
                            case "messaging.operation.type":
                                ActivityInstrumentation.SetLogEventProperty(
                                    activity,
                                    UseDottedPropertyNames ? k : "OperationType",
                                    new ScalarValue(v));
                                break;
                            case "messaging.destination.name":
                                ActivityInstrumentation.SetLogEventProperty(
                                    activity,
                                    UseDottedPropertyNames ? k : "DestinationName",
                                    new ScalarValue(v));
                                break;
                        }
                    }
                }
            }
        }
    }
}
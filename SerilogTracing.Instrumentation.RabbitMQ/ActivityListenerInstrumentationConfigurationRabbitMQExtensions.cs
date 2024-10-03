using SerilogTracing.Configuration;
using SerilogTracing.Instrumentation.RabbitMQ;

namespace SerilogTracing;

public static class ActivityListenerInstrumentationConfigurationRabbitMQExtensions
{
    public static ActivityListenerConfiguration RabbitMQClient(
        this ActivityListenerInstrumentationConfiguration configuration)
    {
        return configuration.With(new RabbitMQClientActivityInstrumentor(new()));
    }

    public static ActivityListenerConfiguration RabbitMQClient(
        this ActivityListenerInstrumentationConfiguration configuration, Action<RabbitMQClientActivityInstrumentationOptions> configure)
    {
        var options = new RabbitMQClientActivityInstrumentationOptions();
        configure.Invoke(options);

        return configuration.With(new RabbitMQClientActivityInstrumentor(options));
    }
}

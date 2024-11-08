using System.Diagnostics;
using Serilog.Events;

namespace SerilogTracing.Instrumentation.RabbitMQ;

/// <summary>
/// Configuration for RabbitMQ Client Subscriber and Publisher instrumentation.
/// </summary>
public sealed class RabbitMQClientActivityInstrumentationOptions
{
    const string DefaultSubscriberMessageTemplate =
        "AMQP {OperationType} {RoutingKey}";

    const string DefaultPublisherMessageTemplate =
        "AMQP {OperationType} {DestinationName}";

    static IEnumerable<LogEventProperty> DefaultGetSubscriberProperties(Activity activity) =>
        new[]
        {
            new LogEventProperty("OperationType",
                new ScalarValue(activity.Tags.FirstOrDefault(x =>
                    string.Equals(x.Key, "messaging.operation.type", StringComparison.OrdinalIgnoreCase)).Value)),
            new LogEventProperty("RoutingKey",
                new ScalarValue(activity.Tags.FirstOrDefault(x =>
                    string.Equals(x.Key, "messaging.rabbitmq.destination.routing_key", StringComparison.OrdinalIgnoreCase)).Value)),
        };

    static IEnumerable<LogEventProperty> DefaultGetPublisherProperties(Activity activity) =>
        new[]
        {
            new LogEventProperty("OperationType",
                new ScalarValue(activity.Tags.FirstOrDefault(x =>
                    string.Equals(x.Key, "messaging.operation.type", StringComparison.OrdinalIgnoreCase)).Value)),
            new LogEventProperty("DestinationName",
                new ScalarValue(activity.Tags.FirstOrDefault(x =>
                    string.Equals(x.Key, "messaging.destination.name", StringComparison.OrdinalIgnoreCase)).Value)),
        };

    /// <summary>
    /// The message template to associate with subscriber activities.
    /// </summary>
    public string SubscriberMessageTemplate { get; set; } = DefaultSubscriberMessageTemplate;

    /// <summary>
    /// The message template to associate with publisher activities.
    /// </summary>
    public string PublisherMessageTemplate { get; set; } = DefaultPublisherMessageTemplate;

    /// <summary>
    /// A function to populate properties on the activity from a subscriber activity.
    /// </summary>
    public Func<Activity, IEnumerable<LogEventProperty>> GetSubscriberProperties { get; set; } = DefaultGetSubscriberProperties;

    /// <summary>
    /// A function to populate properties on the activity from a publisher activity.
    /// </summary>
    public Func<Activity, IEnumerable<LogEventProperty>> GetPublisherProperties { get; set; } = DefaultGetPublisherProperties;
}

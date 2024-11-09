using System.Diagnostics;
using Serilog.Events;

namespace SerilogTracing.Instrumentation.RabbitMQ;

/// <summary>
/// Configuration for RabbitMQ Client Subscriber and Publisher instrumentation.
/// </summary>
/// <remarks>
/// OperationType: Copied from tag "messaging.operation.type". Has values: "send", "process", or "receive".
/// TargetAddress: Path in the form of "/exchanges/{messaging.destination.name}/{messaging.rabbitmq.destination.routing_key}".
/// See https://www.rabbitmq.com/docs/next/amqp#address-v2.
/// </remarks>
public sealed class RabbitMQClientActivityInstrumentationOptions
{
    const string DefaultMessageTemplate =
        "AMQP {OperationType} {TargetAddress}";

    static IEnumerable<LogEventProperty> DefaultGetProperties(Activity activity)
    {
        var exchangePath = new[]
        {
            "exchanges",
            activity.Tags.FirstOrDefault(x =>
                string.Equals(x.Key, "messaging.destination.name", StringComparison.OrdinalIgnoreCase)).Value ?? "",
            activity.Tags.FirstOrDefault(x =>
                string.Equals(x.Key, "messaging.rabbitmq.destination.routing_key", StringComparison.OrdinalIgnoreCase)).Value,
        };
        return new[]
        {
            new LogEventProperty("OperationType",
                new ScalarValue(activity.Tags.FirstOrDefault(x =>
                    string.Equals(x.Key, "messaging.operation.type", StringComparison.OrdinalIgnoreCase)).Value)),
            new LogEventProperty("TargetAddress",
                new ScalarValue("/" + string.Join("/", exchangePath.Where(x => x != null)))),
        };
    }

    /// <summary>
    /// The message template to associate with subscriber activities.
    /// </summary>
    public string SubscriberMessageTemplate { get; set; } = DefaultMessageTemplate;

    /// <summary>
    /// The message template to associate with publisher activities.
    /// </summary>
    public string PublisherMessageTemplate { get; set; } = DefaultMessageTemplate;

    /// <summary>
    /// A function to populate properties on the activity from a subscriber activity.
    /// </summary>
    public Func<Activity, IEnumerable<LogEventProperty>> GetSubscriberProperties { get; set; } = DefaultGetProperties;

    /// <summary>
    /// A function to populate properties on the activity from a publisher activity.
    /// </summary>
    public Func<Activity, IEnumerable<LogEventProperty>> GetPublisherProperties { get; set; } = DefaultGetProperties;
}

using System.Diagnostics;
using Serilog.Events;
using Serilog.Parsing;

namespace SerilogTracing.Instrumentation.RabbitMQ;

/// <summary>
/// An activity instrumentor that populates the current activity a template for the RabbitMQ client
/// </summary>
internal sealed class RabbitMQClientActivityInstrumentor : ActivitySourceInstrumentor
{
    /// <summary>
    /// Create an instance of the instrumentor.
    /// </summary>
    public RabbitMQClientActivityInstrumentor(RabbitMQClientActivityInstrumentationOptions options)
    {
        _getSubscriberProperties = options.GetSubscriberProperties;
        _getPublisherProperties = options.GetPublisherProperties;

        var messageTemplateParser = new MessageTemplateParser();
        _subscriberMessageTemplateOverride = messageTemplateParser.Parse(options.SubscriberMessageTemplate);
        _publisherMessageTemplateOverride = messageTemplateParser.Parse(options.PublisherMessageTemplate);
    }

    private readonly Func<Activity, IEnumerable<LogEventProperty>> _getSubscriberProperties;
    private readonly Func<Activity, IEnumerable<LogEventProperty>> _getPublisherProperties;
    private readonly MessageTemplate _subscriberMessageTemplateOverride;
    private readonly MessageTemplate _publisherMessageTemplateOverride;

    public override bool ShouldSubscribeTo(string activitySourceName)
    {
        return activitySourceName.StartsWith("RabbitMQ.Client", StringComparison.OrdinalIgnoreCase);
    }

    public override void InstrumentOnActivityStopped(Activity activity)
    {
        switch (activity.Source.Name)
        {
            case "RabbitMQ.Client.Subscriber":
                ActivityInstrumentation.SetMessageTemplateOverride(activity, _subscriberMessageTemplateOverride);
                ActivityInstrumentation.SetLogEventProperties(activity, _getSubscriberProperties(activity));
                break;
            case "RabbitMQ.Client.Publisher":
                ActivityInstrumentation.SetMessageTemplateOverride(activity, _publisherMessageTemplateOverride);
                ActivityInstrumentation.SetLogEventProperties(activity, _getPublisherProperties(activity));
                break;
            default:
                throw new NotImplementedException($"No template for the activity source {activity.Source.Name}");
        }
    }
}

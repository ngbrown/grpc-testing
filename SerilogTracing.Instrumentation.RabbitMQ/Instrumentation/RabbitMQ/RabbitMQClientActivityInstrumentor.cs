using System.Diagnostics;

namespace SerilogTracing.Instrumentation.RabbitMQ;

internal sealed class RabbitMQClientActivityInstrumentor(RabbitMQClientActivityInstrumentationOptions options)
    : IActivityInstrumentor, IInstrumentationEventObserver
{
    const string DiagnosticListenerName = "RabbitMQClientDiagnosticListener";

    static readonly ActivitySource ActivitySource = new("SerilogTracing.Instrumentation.RabbitMQClient");

    public bool ShouldSubscribeTo(string diagnosticListenerName)
    {
        return diagnosticListenerName == DiagnosticListenerName;
    }

    public void InstrumentActivity(Activity activity, string eventName, object eventArgs)
    {
        // Instrumentation is applied in `OnDiagnosticEvent`.
    }

    public void OnNext(string eventName, object? eventArgs)
    {
        if (eventArgs == null) return;

        switch (eventName)
        {
            
        }

        throw new NotImplementedException();
    }
}

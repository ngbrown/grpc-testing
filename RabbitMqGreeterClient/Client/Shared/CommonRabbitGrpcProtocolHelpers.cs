namespace RabbitMqGreeterClient.Client.Shared;

internal static class CommonRabbitGrpcProtocolHelpers
{
    // Timer and DateTime.UtcNow have a 14ms precision. Add a small delay when scheduling deadline
    // timer that tests if exceeded or not. This avoids rescheduling the deadline callback multiple
    // times when timer is triggered before DateTime.UtcNow reports the deadline has been exceeded.
    // e.g.
    // - The deadline callback is raised and there is 0.5ms until deadline.
    // - The timer is rescheduled to run in 0.5ms.
    // - The deadline callback is raised again and there is now 0.4ms until deadline.
    // - The timer is rescheduled to run in 0.4ms, etc.
    private static readonly int TimerEpsilonMilliseconds = 14;

    public static long GetTimerDueTime(TimeSpan timeout, long maxTimerDueTime)
    {
        // Timer has a maximum allowed due time.
        // The called method will rechedule the timer if the deadline time has not passed.
        var dueTimeMilliseconds = timeout.Ticks / TimeSpan.TicksPerMillisecond;

        // Add epislon to take into account Timer precision.
        // This will avoid rescheduling the timer multiple times, but means deadline
        // might run slightly longer than requested.
        dueTimeMilliseconds += TimerEpsilonMilliseconds;

        dueTimeMilliseconds = Math.Min(dueTimeMilliseconds, maxTimerDueTime);
        // Timer can't have a negative due time
        dueTimeMilliseconds = Math.Max(dueTimeMilliseconds, 0);

        return dueTimeMilliseconds;
    }
}
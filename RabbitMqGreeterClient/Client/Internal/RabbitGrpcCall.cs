using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using Google.Protobuf;
using Grpc.Core;
using RabbitMqGreeterClient.Client.Shared;
using RabbitRpc.Core;
using IMethod = Grpc.Core.IMethod;

namespace RabbitMqGreeterClient.Client.Internal;

internal class RabbitGrpcCall<TRequest, TResponse> : RabbitGrpcCall, IRabbitGrpcCall<TRequest, TResponse> where TRequest : class where TResponse : class
{
    internal const string ErrorStartingCallMessage = "Error starting Rabbit RPC call.";

    private readonly CancellationTokenSource _callCts;
    private readonly TaskCompletionSource<RabbitRpcResponse> _rpcResponseTcs;
    private readonly TaskCompletionSource<Status> _callTcs;
    private readonly int _attemptCount;

    private Timer? _deadlineTimer;
    private DateTime _deadline;
    private CancellationTokenRegistration? _ctsRegistration;

    public bool Disposed { get; private set; }
    public Method<TRequest, TResponse> Method { get; }

    // These are set depending on the type of gRPC call
    private TaskCompletionSource<TResponse>? _responseTcs;

    public RabbitGrpcCall(Method<TRequest, TResponse> method, CallOptions options, RabbitRpcChannel channel, int attemptCount)
        : base(options, channel)
    {
        _callCts = new CancellationTokenSource();
        _rpcResponseTcs = new TaskCompletionSource<RabbitRpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        // Run the callTcs continuation immediately to keep the same context. Required for Activity.
        _callTcs = new TaskCompletionSource<Status>();
        Method = method;
        _deadline = options.Deadline ?? DateTime.MaxValue;
        _attemptCount = attemptCount;
    }

    public override Task<Status> CallTask => _callTcs.Task;
    
    public override CancellationToken CancellationToken => _callCts.Token;


    public object? CallWrapper { get; set; }

    MethodType IMethod.Type => Method.Type;
    string IMethod.ServiceName => Method.ServiceName;
    string IMethod.Name => Method.Name;
    string IMethod.FullName => Method.FullName;

    public void StartUnary(TRequest request) => StartUnaryCore(CreatePushUnaryContent(request));

    private ByteString? CreatePushUnaryContent(TRequest request)
    {
        ByteString? content = null;

        Method.RequestMarshaller.ContextualSerializer(request, new BodyStringSerializationContext(SetContent));

        return content;

        void SetContent(ByteString newContent)
        {
            content = newContent;
        }
    }

    private void StartUnaryCore(ByteString? content)
    {
        _responseTcs = new TaskCompletionSource<TResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

        var timeout = GetTimeout();
        var message = CreateRabbitRpcRequestMessage(timeout);
        SetMessageContent(content, message);
        _ = RunCall(message, timeout);
    }

    public void Dispose()
    {
        Disposed = true;
        _responseTcs?.Task.ObserveException();
    }

    /// <summary>
    /// Clean up can be called by:
    /// 1. The user. AsyncUnaryCall.Dispose et al will call this on Dispose
    /// 3. <see cref="FinishResponseAndCleanUp"/> will call dispose
    /// </summary>
    private void Cleanup(Status status)
    {
        if (!ResponseFinished)
        {
            CancelCall(status);
        }
        else
        {
            _callTcs.TrySetResult(status);
        }

        _ctsRegistration?.Dispose();

        if (_deadlineTimer != null)
        {
            lock (this)
            {
                // Timer callback can call Timer.Change so dispose deadline timer in a lock
                // and set to null to indicate to the callback that it has been disposed.
                _deadlineTimer?.Dispose();
                _deadlineTimer = null;
            }
        }

        // To avoid racing with Dispose, skip disposing the call CTS.
        // This avoid Dispose potentially calling cancel on a disposed CTS.
        // The call CTS is not exposed externally and all dependent registrations
        // are cleaned up.
    }

    private void FinishResponseAndCleanUp(Status status)
    {
        ResponseFinished = true;

        // Clean up call resources once this call is finished
        // Call may not be explicitly disposed when used with unary methods
        // e.g. var reply = await client.SayHelloAsync(new HelloRequest());
        Cleanup(status);
    }

    public Task<Metadata> GetResponseHeadersAsync()
    {
        throw new NotImplementedException();
    }

    public Status GetStatus()
    {
        if (CallTask.IsCompletedSuccessfully())
        {
            return CallTask.Result;
        }

        throw new InvalidOperationException("Unable to get the status because the call is not complete.");
    }

    public Task<TResponse> GetResponseAsync()
    {
        Debug.Assert(_responseTcs != null, nameof(_responseTcs) + " != null");
        return _responseTcs.Task;
    }

    public Metadata GetTrailers()
    {
        throw new NotImplementedException();
    }
    
    private void SetMessageContent(ByteString? content, RabbitRpcRequest message)
    {
        message.Body = content;
    }

    private CancellationTokenRegistration RegisterCancellation(CancellationToken cancellationToken)
    {
        return CompatibilityHelpers.RegisterWithCancellationTokenCallback(
            cancellationToken,
            static (state, ct) =>
            {
                var call = (RabbitGrpcCall<TRequest, TResponse>)state!;
                call.CancelCallFromCancellationToken(ct);
            },
            this);
    }
    
    private void CancelCallFromCancellationToken(CancellationToken cancellationToken)
    {
        //using (StartScope())
        {
            CancelCall(RabbitGrpcProtocolConstants.CreateClientCanceledStatus(new OperationCanceledException(cancellationToken)));
        }
    }

    private void CancelCall(Status status)
    {
// Set overall call status first. Status can be used in throw RpcException from cancellation.
        // If response has successfully finished then the status will come from the trailers.
        // If it didn't finish then complete with a status.
        _callTcs.TrySetResult(status);

// Checking if cancellation has already happened isn't threadsafe
        // but there is no adverse effect other than an extra log message
        if (!_callCts.IsCancellationRequested)
        {
            // Cancel in-progress HttpClient.SendAsync and Stream.ReadAsync tasks.
            // Cancel will send RST_STREAM if HttpClient.SendAsync isn't complete.
            // Cancellation will also cause reader/writer to throw if used afterwards.
            _callCts.Cancel();

            // Ensure any logic that is waiting on the RpcResponse is unstuck.
            _rpcResponseTcs.TrySetCanceled();
        }
    }

    private async Task RunCall(RabbitRpcRequest request, TimeSpan? timeout)
    {
        InitializeCall(request, timeout);

        Status? status = null;

        try
        {
            // Fail early if deadline has already been exceeded
            _callCts.Token.ThrowIfCancellationRequested();

            try
            {
                var rabbitResponseTask = Channel.CallAsync(request.ToByteArray(), _callCts.Token);

                RpcResponse = await rabbitResponseTask.ConfigureAwait(false);
                _rpcResponseTcs.TrySetResult(RpcResponse);
            }
            catch (Exception ex)
            {
                throw;
            }

            status = ValidateHeaders(RpcResponse);

            if (status != null)
            {
                if (_responseTcs != null)
                {
                    var message =
                        Method.ResponseMarshaller.ContextualDeserializer(new ByteStringDeserializationContext(RpcResponse.Body));

                    if (message == null)
                    {
                        // Change the status code if OK is returned to a more accurate status.
                        // This is consistent with Grpc.Core client behavior.
                        status = new Status(StatusCode.Internal, "Failed to deserialize response message.");

                        FinishResponseAndCleanUp(status.Value);

                        SetFailedResult(status.Value);
                    }
                    else
                    {
                        FinishResponseAndCleanUp(status.Value);

                        if (status.Value.StatusCode == StatusCode.OK)
                        {
                            _responseTcs.TrySetResult(message);
                        }
                        else
                        {
                            // Set failed result makes the response task thrown an error. Must be called after
                            // the response is finished. Reasons:
                            // - Finishing the response sets the status. Required for GetStatus to be successful.
                            // - We want GetStatus to always work when called after the response task is done.
                            SetFailedResult(status.Value);
                        }
                    }
                }
                else
                {
                    // Duplex or server streaming call

                    status = new Status(StatusCode.Internal, "Not Implemented");

                    Cleanup(status.Value);
                    SetFailedResult(status.Value);
                }
            }
            else
            {
                status = new Status(StatusCode.Internal, "Not Implemented");

                Cleanup(status.Value);
                SetFailedResult(status.Value);
            }
        }
        catch (Exception ex)
        {
            ResolveException(ErrorStartingCallMessage, ex, out status, out var resolvedException);

            // Update RPC response TCS before clean up. Needs to happen first because cleanup will
            // cancel the TCS for anyone still listening.
            _rpcResponseTcs.TrySetException(resolvedException);
            _rpcResponseTcs.Task.ObserveException();

            Cleanup(status.Value);

            // Update response TCS after overall call status is resolved. This is required so that
            // the call is completed before an error is thrown from ResponseAsync. If it happens
            // afterwards then there is a chance GetStatus() will error because the call isn't complete.
            if (_responseTcs != null)
            {
                _responseTcs.TrySetException(resolvedException);

                // Always observe cancellation-like exceptions.
                if (IsCancellationOrDeadlineException(ex))
                {
                    _responseTcs.Task.ObserveException();
                }
            }
        }
    }

    private bool IsCancellationOrDeadlineException(Exception ex)
    {
        // Don't log OperationCanceledException if deadline has exceeded
        // or the call has been canceled.
        if (ex is OperationCanceledException &&
            _callTcs.Task.IsCompletedSuccessfully() &&
            (_callTcs.Task.Result.StatusCode == StatusCode.DeadlineExceeded || _callTcs.Task.Result.StatusCode == StatusCode.Cancelled))
        {
            return true;
        }

        // Exception may specify RST_STREAM or abort code that resolves to cancellation.
        // If protocol error is cancellation and deadline has been exceeded then that
        // means the server canceled and the local deadline timer hasn't triggered.
        if (RabbitGrpcProtocolHelpers.ResolveRpcExceptionStatusCode(ex) == StatusCode.Cancelled)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resolve the specified exception to an end-user exception that will be thrown from the client.
    /// The resolved exception is normally a RpcException. Returns true when the resolved exception is changed.
    /// </summary>
    internal bool ResolveException(string summary, Exception ex, [NotNull] out Status? status, out Exception resolvedException)
    {
        if (ex is OperationCanceledException)
        {
            ex = EnsureUserCancellationTokenReported(ex, CancellationToken.None);
            status = (CallTask.IsCompletedSuccessfully()) ? CallTask.Result : new Status(StatusCode.Cancelled, string.Empty, ex);
        }
        else
        {
            var s = RabbitGrpcProtocolHelpers.CreateStatusFromException(summary, ex);
            
            // The server could exceed the deadline and return a CANCELLED status before the
            // client's deadline timer is triggered. When CANCELLED is received check the
            // deadline against the clock and change status to DEADLINE_EXCEEDED if required.
            if (s.StatusCode == StatusCode.Cancelled)
            {
                lock (this)
                {
                    if (IsDeadlineExceededUnsynchronized())
                    {
                        s = new Status(StatusCode.DeadlineExceeded, s.Detail, s.DebugException);
                    }
                }
            }

            status = s;
        }

        resolvedException = ex;
        return false;
    }

    // Report correct CancellationToken. This method creates a new OperationCanceledException with a different CancellationToken.
    // It attempts to preserve the original stack trace using ExceptionDispatchInfo where available.
#if NET6_0_OR_GREATER
    [StackTraceHidden]
#endif
    public Exception EnsureUserCancellationTokenReported(Exception ex, CancellationToken cancellationToken)
    {
        var token = GetCanceledToken(cancellationToken);
        if (token != CancellationToken)
        {
#if NET6_0_OR_GREATER
            return ExceptionDispatchInfo.SetCurrentStackTrace(new OperationCanceledException(ex.Message, ex, token));
#else
            return new OperationCanceledException(ex.Message, ex, token);
#endif
        }

        return ex;
    }

    private void SetFailedResult(Status status)
    {
        Debug.Assert(_responseTcs != null);

        if (status.StatusCode == StatusCode.DeadlineExceeded)
        {
            // Convert status response of DeadlineExceeded to OperationCanceledException when
            // ThrowOperationCanceledOnCancellation is true.
            // This avoids a race between the client-side timer and the server status throwing different
            // errors on deadline exceeded.
            _responseTcs.TrySetCanceled();
        }
        else
        {
            _responseTcs.TrySetException(new Exception(status.ToString()));
        }
    }

    private void InitializeCall(RabbitRpcRequest request, TimeSpan? timeout)
    {
        // Deadline will cancel the call CTS.
        // Only exceed deadline/start timer after reader/writer have been created, otherwise deadline will cancel
        // the call CTS before they are created and leave them in a non-canceled state.
        if (timeout != null && !Channel.DisableClientDeadline)
        {
            if (timeout.Value <= TimeSpan.Zero)
            {
                // Call was started with a deadline in the past so immediately trigger deadline exceeded.
                lock (this)
                {
                    DeadlineExceeded();
                }
            }
            else
            {
                //GrpcCallLog.StartingDeadlineTimeout(Logger, timeout.Value);

                var dueTime = CommonRabbitGrpcProtocolHelpers.GetTimerDueTime(timeout.Value, Channel.MaxTimerDueTime);
                _deadlineTimer = NonCapturingTimer.Create(DeadlineExceededCallback, state: null, TimeSpan.FromMilliseconds(dueTime), Timeout.InfiniteTimeSpan);
            }
        }

        if (Options.CancellationToken.CanBeCanceled)
        {
            // The cancellation token will cancel the call CTS.
            // This must be registered after the client writer has been created
            // so that cancellation will always complete the writer.
            _ctsRegistration = RegisterCancellation(Options.CancellationToken);
        }
    }

    private bool IsDeadlineExceededUnsynchronized()
    {
        Debug.Assert(Monitor.IsEntered(this), "Check deadline in a lock. Updating a DateTime isn't guaranteed to be atomic. Avoid struct tearing.");
        return _deadline <= DateTime.UtcNow; // Channel.Clock.UtcNow;
    }
    
    private RabbitRpcRequest CreateRabbitRpcRequestMessage(TimeSpan? timeout)
    {
        var rabbitRequest = new RabbitRpcRequest
        {
            ServiceName = Method.ServiceName,
            MethodName = Method.FullName,
            Body = ByteString.CopyFrom(),
        };

        if (Options.Headers != null)
        {
            rabbitRequest.Headers.AddRange(
                Options.Headers.Select(x => new RabbitRpcHeader { Key = x.Key, Value = x.Value }));
        }

        return rabbitRequest;
    }

    private TimeSpan? GetTimeout()
    {
        if (_deadline == DateTime.MaxValue)
        {
            return null;
        }

        var timeout = _deadline - DateTime.UtcNow; //_deadline - Channel.Clock.UtcNow;

        // Maxmimum deadline of 99999999s is consistent with Grpc.Core
        // https://github.com/grpc/grpc/blob/907a1313a87723774bf59d04ed432602428245c3/src/core/lib/transport/timeout_encoding.h#L32-L34
        const long MaxDeadlineTicks = 99999999 * TimeSpan.TicksPerSecond;

        if (timeout.Ticks > MaxDeadlineTicks)
        {
            //GrpcCallLog.DeadlineTimeoutTooLong(Logger, timeout);

            timeout = TimeSpan.FromTicks(MaxDeadlineTicks);
        }

        return timeout;
    }

    private void DeadlineExceededCallback(object? state)
    {
        try
        {
            // Deadline is only exceeded if the timeout has passed and
            // the response has not been finished or canceled
            if (!_callCts.IsCancellationRequested && !ResponseFinished)
            {
                TimeSpan remaining;
                lock (this)
                {
                    // If _deadline is MaxValue then the DEADLINE_EXCEEDED status has
                    // already been received by the client and the timer can stop.
                    if (_deadline == DateTime.MaxValue)
                    {
                        return;
                    }

                    remaining = _deadline - DateTime.UtcNow; // _deadline - Channel.Clock.UtcNow;
                    if (remaining <= TimeSpan.Zero)
                    {
                        DeadlineExceeded();
                        return;
                    }

                    if (_deadlineTimer != null)
                    {
                        // Deadline has not been reached because timer maximum due time was smaller than deadline.
                        // Reschedule DeadlineExceeded again until deadline has been exceeded.
                        //GrpcCallLog.DeadlineTimerRescheduled(Logger, remaining);

                        var dueTime = CommonRabbitGrpcProtocolHelpers.GetTimerDueTime(remaining, Channel.MaxTimerDueTime);
                        _deadlineTimer.Change(dueTime, Timeout.Infinite);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Ensure exceptions are never thrown from a timer.
            //GrpcCallLog.ErrorExceedingDeadline(Logger, ex);
        }
    }

    private void DeadlineExceeded()
    {
        Debug.Assert(Monitor.IsEntered(this));

        // Set _deadline to DateTime.MaxValue to signal that deadline has been exceeded.
        // This prevents duplicate logging and cancellation.
        _deadline = DateTime.MaxValue;

        CancelCall(new Status(StatusCode.DeadlineExceeded, string.Empty));
    }
}
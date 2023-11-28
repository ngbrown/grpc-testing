using System.Globalization;
using System.Text;

namespace GrpcGreeter;

public class FibService
{
    private readonly ILogger<FibService> _logger;

    public FibService(ILogger<FibService> logger)
    {
        _logger = logger;
    }

    public async Task<byte[]> GetFibAsync(byte[] body, CancellationToken cancellationToken)
    {
        //await Task.Delay(100, cancellationToken).ConfigureAwait(false);

        string response;
        int? n = default;
        try
        {
            var requestMessage = Encoding.UTF8.GetString(body);
            n = int.Parse(requestMessage);
            _logger.LogInformation("Fib({n})", n);
            response = Fib(n.Value, cancellationToken).ToString(CultureInfo.InvariantCulture);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning("Fib({n}) - {message}", n, ex.Message);
            response = string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fib({n}) - {message}", n, ex.Message);
            response = string.Empty;
        }

        return Encoding.UTF8.GetBytes(response);
    }

    /// <remarks>
    /// Assumes only valid positive integer input.
    /// Don't expect this one to work for big numbers, and it's probably the slowest recursive implementation possible.
    /// </remarks>
    private static int Fib(int n, CancellationToken cancellationToken)
    {
        if (n is 0 or 1)
        {
            return n;
        }

        cancellationToken.ThrowIfCancellationRequested();

        return Fib(n - 1, cancellationToken) + Fib(n - 2, cancellationToken);
    }
}
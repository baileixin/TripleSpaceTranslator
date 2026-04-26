namespace TripleSpaceTranslator.Core.Models;

public sealed class ConnectionTestResult
{
    public bool Succeeded { get; init; }

    public string Message { get; init; } = string.Empty;

    public string ErrorCode { get; init; } = string.Empty;

    public long ResponseTimeMs { get; init; }

    public static ConnectionTestResult Success(long responseTimeMs, string message = "连接成功。")
    {
        return new ConnectionTestResult
        {
            Succeeded = true,
            Message = message,
            ResponseTimeMs = responseTimeMs
        };
    }

    public static ConnectionTestResult Failure(string errorCode, string message, long responseTimeMs = 0)
    {
        return new ConnectionTestResult
        {
            Succeeded = false,
            ErrorCode = errorCode,
            Message = message,
            ResponseTimeMs = responseTimeMs
        };
    }
}

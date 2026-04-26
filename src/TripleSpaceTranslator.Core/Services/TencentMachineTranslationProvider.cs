using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using TripleSpaceTranslator.Core.Interfaces;
using TripleSpaceTranslator.Core.Models;
using TripleSpaceTranslator.Core.Utilities;

namespace TripleSpaceTranslator.Core.Services;

public sealed class TencentMachineTranslationProvider : ITranslationProvider
{
    private const string ActionName = "TextTranslate";
    private const string ServiceName = "tmt";
    private const string Version = "2018-03-21";

    private readonly TranslationProviderConfig _config;
    private readonly HttpClient _httpClient;
    private readonly Func<DateTimeOffset> _utcNowProvider;

    public TencentMachineTranslationProvider(
        HttpClient httpClient,
        TranslationProviderConfig config,
        Func<DateTimeOffset>? utcNowProvider = null)
    {
        _httpClient = httpClient;
        _config = config.Clone();
        _utcNowProvider = utcNowProvider ?? (() => DateTimeOffset.UtcNow);
    }

    public TranslationProviderType ProviderType => TranslationProviderType.TencentMachineTranslation;

    public async Task<string> TranslateAsync(string sourceText, string targetLanguage, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return string.Empty;
        }

        EnsureCredentials();

        var normalizedTarget = TencentLanguageCodeMapper.Normalize(targetLanguage);
        var detectedSource = SourceLanguageHeuristics.DetectTencentLanguageCode(sourceText);
        if (string.Equals(detectedSource, normalizedTarget, StringComparison.OrdinalIgnoreCase))
        {
            return sourceText;
        }

        var requestBody = JsonSerializer.Serialize(new
        {
            SourceText = sourceText,
            Source = detectedSource,
            Target = normalizedTarget,
            ProjectId = _config.ProjectId
        });

        using var request = BuildSignedRequest(requestBody, _config.Region);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw CreateHttpException(response.StatusCode, responseText);
        }

        using var document = JsonDocument.Parse(responseText);
        var responseElement = document.RootElement.TryGetProperty("Response", out var nestedResponse)
            ? nestedResponse
            : document.RootElement;

        if (TryThrowServiceError(responseElement, out var serviceException))
        {
            throw serviceException;
        }

        if (responseElement.TryGetProperty("TargetText", out var targetTextElement))
        {
            return targetTextElement.GetString() ?? string.Empty;
        }

        throw new TranslationProviderException("invalid_response", "腾讯云翻译接口返回了无法识别的数据。");
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(TranslationProviderConfig providerConfig, string targetLanguage, CancellationToken cancellationToken)
    {
        var sanitized = providerConfig.Clone();
        if (string.IsNullOrWhiteSpace(sanitized.SecretId) || string.IsNullOrWhiteSpace(sanitized.SecretKey))
        {
            return ConnectionTestResult.Failure("missing_credentials", "请先填写腾讯云 SecretId 和 SecretKey。");
        }

        var probe = BuildProbe(targetLanguage);

        try
        {
            var provider = new TencentMachineTranslationProvider(_httpClient, sanitized, _utcNowProvider);
            var startedAt = _utcNowProvider();
            var translated = await provider.TranslateKnownSourceAsync(
                probe.SourceText,
                probe.SourceLanguage,
                probe.TargetLanguage,
                cancellationToken).ConfigureAwait(false);
            var elapsed = (_utcNowProvider() - startedAt).TotalMilliseconds;

            return string.IsNullOrWhiteSpace(translated)
                ? ConnectionTestResult.Failure("empty_result", "腾讯云翻译接口返回为空。", (long)elapsed)
                : ConnectionTestResult.Success((long)elapsed);
        }
        catch (TranslationProviderException ex)
        {
            return ConnectionTestResult.Failure(ex.ErrorCode, ex.Message);
        }
        catch (HttpRequestException)
        {
            return ConnectionTestResult.Failure("network_error", "无法连接到腾讯云翻译服务。");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ConnectionTestResult.Failure("timeout", "连接超时，请稍后重试。");
        }
        catch (Exception ex)
        {
            return ConnectionTestResult.Failure("unexpected_error", $"测试失败：{ex.Message}");
        }
    }

    private async Task<string> TranslateKnownSourceAsync(
        string sourceText,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken)
    {
        EnsureCredentials();

        var requestBody = JsonSerializer.Serialize(new
        {
            SourceText = sourceText,
            Source = TencentLanguageCodeMapper.Normalize(sourceLanguage),
            Target = TencentLanguageCodeMapper.Normalize(targetLanguage),
            ProjectId = _config.ProjectId
        });

        using var request = BuildSignedRequest(requestBody, _config.Region);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw CreateHttpException(response.StatusCode, responseText);
        }

        using var document = JsonDocument.Parse(responseText);
        var responseElement = document.RootElement.TryGetProperty("Response", out var nestedResponse)
            ? nestedResponse
            : document.RootElement;

        if (TryThrowServiceError(responseElement, out var serviceException))
        {
            throw serviceException;
        }

        return responseElement.TryGetProperty("TargetText", out var targetTextElement)
            ? targetTextElement.GetString() ?? string.Empty
            : string.Empty;
    }

    private HttpRequestMessage BuildSignedRequest(string requestBody, string region)
    {
        var endpointUri = BuildEndpointUri();
        var host = endpointUri.Host;
        var timestamp = _utcNowProvider().ToUnixTimeSeconds();
        var authorization = TencentCloudApiSigner.CreateAuthorization(
            _config.SecretId.Trim(),
            _config.SecretKey.Trim(),
            ServiceName,
            host,
            ActionName,
            requestBody,
            timestamp,
            out _);

        var request = new HttpRequestMessage(HttpMethod.Post, endpointUri)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };
        request.Content.Headers.ContentType!.CharSet = "utf-8";
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        request.Headers.TryAddWithoutValidation("X-TC-Action", ActionName);
        request.Headers.TryAddWithoutValidation("X-TC-Version", Version);
        request.Headers.TryAddWithoutValidation("X-TC-Timestamp", timestamp.ToString());
        request.Headers.TryAddWithoutValidation("X-TC-Region", region.Trim());
        request.Headers.Host = host;

        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_config.TimeoutSeconds, 1, 30)));

        try
        {
            return await _httpClient.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TranslationProviderException("timeout", "连接超时，请稍后重试。");
        }
        catch (HttpRequestException)
        {
            throw new TranslationProviderException("network_error", "无法连接到腾讯云翻译服务。");
        }
    }

    private Uri BuildEndpointUri()
    {
        var endpoint = string.IsNullOrWhiteSpace(_config.Endpoint)
            ? "https://tmt.tencentcloudapi.com/"
            : _config.Endpoint.Trim();

        if (!endpoint.EndsWith("/", StringComparison.Ordinal))
        {
            endpoint += "/";
        }

        return new Uri(endpoint, UriKind.Absolute);
    }

    private void EnsureCredentials()
    {
        if (string.IsNullOrWhiteSpace(_config.SecretId) || string.IsNullOrWhiteSpace(_config.SecretKey))
        {
            throw new TranslationProviderException("missing_credentials", "请先填写腾讯云 SecretId 和 SecretKey。");
        }
    }

    private static bool TryThrowServiceError(JsonElement responseElement, out TranslationProviderException serviceException)
    {
        serviceException = null!;

        if (!responseElement.TryGetProperty("Error", out var errorElement))
        {
            return false;
        }

        var errorCode = errorElement.TryGetProperty("Code", out var codeElement)
            ? codeElement.GetString() ?? "request_failed"
            : "request_failed";
        var message = errorElement.TryGetProperty("Message", out var messageElement)
            ? messageElement.GetString() ?? "腾讯云翻译接口返回错误。"
            : "腾讯云翻译接口返回错误。";

        serviceException = MapServiceError(errorCode, message);
        return true;
    }

    private static TranslationProviderException CreateHttpException(HttpStatusCode statusCode, string responseText)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                new TranslationProviderException("authentication_failed", "腾讯云鉴权失败，请检查 SecretId、SecretKey 和系统时间。"),
            HttpStatusCode.TooManyRequests =>
                new TranslationProviderException("rate_limited", "请求过于频繁，请稍后重试。"),
            HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout or HttpStatusCode.ServiceUnavailable or HttpStatusCode.InternalServerError =>
                new TranslationProviderException("service_error", "腾讯云翻译服务暂时不可用，请稍后重试。"),
            _ => new TranslationProviderException("request_failed", $"腾讯云翻译请求失败：{responseText}")
        };
    }

    private static TranslationProviderException MapServiceError(string errorCode, string message)
    {
        if (errorCode.StartsWith("AuthFailure", StringComparison.OrdinalIgnoreCase))
        {
            return new TranslationProviderException("authentication_failed", message);
        }

        if (errorCode.Contains("RequestLimitExceeded", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("LimitExceeded", StringComparison.OrdinalIgnoreCase))
        {
            return new TranslationProviderException("rate_limited", message);
        }

        if (string.Equals(errorCode, "FailedOperation.UserNotRegistered", StringComparison.OrdinalIgnoreCase))
        {
            return new TranslationProviderException("service_not_enabled", message);
        }

        if (string.Equals(errorCode, "FailedOperation.ServiceIsolate", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(errorCode, "FailedOperation.StopUsing", StringComparison.OrdinalIgnoreCase))
        {
            return new TranslationProviderException("service_unavailable", message);
        }

        if (string.Equals(errorCode, "FailedOperation.LanguageRecognitionErr", StringComparison.OrdinalIgnoreCase))
        {
            return new TranslationProviderException("language_recognition_failed", message);
        }

        if (errorCode.StartsWith("InternalError", StringComparison.OrdinalIgnoreCase))
        {
            return new TranslationProviderException("service_error", message);
        }

        return new TranslationProviderException("request_failed", message);
    }

    private static (string SourceText, string SourceLanguage, string TargetLanguage) BuildProbe(string targetLanguage)
    {
        var normalizedTarget = TencentLanguageCodeMapper.Normalize(targetLanguage);
        return string.Equals(normalizedTarget, "en", StringComparison.OrdinalIgnoreCase)
            ? ("你好", "zh", "en")
            : ("hello", "en", normalizedTarget);
    }
}

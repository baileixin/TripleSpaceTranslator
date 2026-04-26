using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TripleSpaceTranslator.Core.Interfaces;
using TripleSpaceTranslator.Core.Models;
using TripleSpaceTranslator.Core.Utilities;

namespace TripleSpaceTranslator.Core.Services;

public sealed class BaiduGeneralTextTranslationProvider : ITranslationProvider
{
    private const string DefaultEndpoint = "https://fanyi-api.baidu.com/api/trans/vip/translate";

    private readonly TranslationProviderConfig _config;
    private readonly HttpClient _httpClient;
    private readonly Func<string> _saltProvider;

    public BaiduGeneralTextTranslationProvider(
        HttpClient httpClient,
        TranslationProviderConfig config,
        Func<string>? saltProvider = null)
    {
        _httpClient = httpClient;
        _config = config.Clone();
        _saltProvider = saltProvider ?? (() => Guid.NewGuid().ToString("N"));
    }

    public TranslationProviderType ProviderType => TranslationProviderType.BaiduGeneralTextTranslation;

    public async Task<string> TranslateAsync(string sourceText, string targetLanguage, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return string.Empty;
        }

        EnsureCredentials();

        var normalizedTarget = BaiduLanguageCodeMapper.Normalize(targetLanguage);
        var detectedSource = SourceLanguageHeuristics.DetectBaiduLanguageCode(sourceText);
        if (string.Equals(detectedSource, normalizedTarget, StringComparison.OrdinalIgnoreCase))
        {
            return sourceText;
        }

        return await TranslateKnownSourceAsync(sourceText, detectedSource, normalizedTarget, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(TranslationProviderConfig providerConfig, string targetLanguage, CancellationToken cancellationToken)
    {
        var sanitized = providerConfig.Clone();
        if (string.IsNullOrWhiteSpace(sanitized.SecretId) || string.IsNullOrWhiteSpace(sanitized.SecretKey))
        {
            return ConnectionTestResult.Failure("missing_credentials", "请先填写百度翻译 AppId 和 AppKey。");
        }

        var probe = BuildProbe(targetLanguage);
        var provider = new BaiduGeneralTextTranslationProvider(_httpClient, sanitized, _saltProvider);
        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            var translated = await provider.TranslateKnownSourceAsync(
                probe.SourceText,
                probe.SourceLanguage,
                probe.TargetLanguage,
                cancellationToken).ConfigureAwait(false);
            var elapsed = (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;

            return string.IsNullOrWhiteSpace(translated)
                ? ConnectionTestResult.Failure("empty_result", "百度翻译接口返回为空。", (long)elapsed)
                : ConnectionTestResult.Success((long)elapsed);
        }
        catch (TranslationProviderException ex)
        {
            return ConnectionTestResult.Failure(ex.ErrorCode, ex.Message);
        }
        catch (HttpRequestException)
        {
            return ConnectionTestResult.Failure("network_error", "无法连接到百度翻译服务。");
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

        using var request = BuildRequest(sourceText, sourceLanguage, targetLanguage);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw CreateHttpException(response.StatusCode, responseText);
        }

        using var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;

        if (root.TryGetProperty("error_code", out var errorCodeElement))
        {
            var errorCode = errorCodeElement.GetString() ?? "request_failed";
            var message = root.TryGetProperty("error_msg", out var errorMessageElement)
                ? errorMessageElement.GetString() ?? "百度翻译接口返回错误。"
                : "百度翻译接口返回错误。";

            throw MapServiceError(errorCode, message);
        }

        if (!root.TryGetProperty("trans_result", out var transResultElement) ||
            transResultElement.ValueKind != JsonValueKind.Array)
        {
            throw new TranslationProviderException("invalid_response", "百度翻译接口返回了无法识别的数据。");
        }

        var translatedSegments = new List<string>();
        foreach (var item in transResultElement.EnumerateArray())
        {
            if (item.TryGetProperty("dst", out var dstElement))
            {
                translatedSegments.Add(dstElement.GetString() ?? string.Empty);
            }
        }

        return string.Join(Environment.NewLine, translatedSegments);
    }

    private HttpRequestMessage BuildRequest(string sourceText, string sourceLanguage, string targetLanguage)
    {
        var appId = _config.SecretId.Trim();
        var salt = _saltProvider();
        var sign = CreateSign(appId, sourceText, salt, _config.SecretKey.Trim());
        var payload = new Dictionary<string, string>
        {
            ["q"] = sourceText,
            ["from"] = BaiduLanguageCodeMapper.Normalize(sourceLanguage),
            ["to"] = BaiduLanguageCodeMapper.Normalize(targetLanguage),
            ["appid"] = appId,
            ["salt"] = salt,
            ["sign"] = sign
        };

        return new HttpRequestMessage(HttpMethod.Post, BuildEndpointUri())
        {
            Content = new FormUrlEncodedContent(payload)
        };
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
            throw new TranslationProviderException("network_error", "无法连接到百度翻译服务。");
        }
    }

    private Uri BuildEndpointUri()
    {
        var endpoint = string.IsNullOrWhiteSpace(_config.Endpoint) ||
                       _config.Endpoint.Contains("tencentcloudapi.com", StringComparison.OrdinalIgnoreCase)
            ? DefaultEndpoint
            : _config.Endpoint.Trim();
        return new Uri(endpoint, UriKind.Absolute);
    }

    private void EnsureCredentials()
    {
        if (string.IsNullOrWhiteSpace(_config.SecretId) || string.IsNullOrWhiteSpace(_config.SecretKey))
        {
            throw new TranslationProviderException("missing_credentials", "请先填写百度翻译 AppId 和 AppKey。");
        }
    }

    private static string CreateSign(string appId, string sourceText, string salt, string secretKey)
    {
        var raw = $"{appId}{sourceText}{salt}{secretKey}";
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static TranslationProviderException CreateHttpException(HttpStatusCode statusCode, string responseText)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                new TranslationProviderException("authentication_failed", "百度翻译鉴权失败，请检查 AppId 和 AppKey。"),
            HttpStatusCode.TooManyRequests =>
                new TranslationProviderException("rate_limited", "请求过于频繁，请稍后重试。"),
            HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout or HttpStatusCode.ServiceUnavailable or HttpStatusCode.InternalServerError =>
                new TranslationProviderException("service_error", "百度翻译服务暂时不可用，请稍后重试。"),
            _ => new TranslationProviderException("request_failed", $"百度翻译请求失败：{responseText}")
        };
    }

    private static TranslationProviderException MapServiceError(string errorCode, string message)
    {
        return errorCode switch
        {
            "52001" => new TranslationProviderException("timeout", "请求超时，请稍后重试。"),
            "52002" => new TranslationProviderException("service_error", "百度翻译服务暂时不可用，请稍后重试。"),
            "52003" => new TranslationProviderException("authentication_failed", "百度翻译鉴权失败，请检查 AppId 和 AppKey。"),
            "54000" => new TranslationProviderException("invalid_request", "请求参数不完整，请检查翻译配置。"),
            "54001" => new TranslationProviderException("authentication_failed", "签名错误，请检查 AppId 和 AppKey。"),
            "54003" => new TranslationProviderException("rate_limited", "请求过于频繁，请稍后重试。"),
            "54004" => new TranslationProviderException("quota_exhausted", "账号余额不足或免费额度已用完。"),
            "54005" => new TranslationProviderException("rate_limited", "长文本请求过于频繁，请稍后重试。"),
            "58000" => new TranslationProviderException("access_denied", "当前客户端 IP 未被允许访问百度翻译接口。"),
            "58001" => new TranslationProviderException("unsupported_language", "当前语言方向不受百度翻译支持。"),
            _ => new TranslationProviderException("request_failed", message)
        };
    }

    private static (string SourceText, string SourceLanguage, string TargetLanguage) BuildProbe(string targetLanguage)
    {
        var normalizedTarget = BaiduLanguageCodeMapper.Normalize(targetLanguage);
        return string.Equals(normalizedTarget, "en", StringComparison.OrdinalIgnoreCase)
            ? ("你好", "zh", "en")
            : ("hello", "en", normalizedTarget);
    }
}

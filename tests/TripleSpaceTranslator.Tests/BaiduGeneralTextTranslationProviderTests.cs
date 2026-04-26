using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TripleSpaceTranslator.Core.Models;
using TripleSpaceTranslator.Core.Services;

namespace TripleSpaceTranslator.Tests;

public sealed class BaiduGeneralTextTranslationProviderTests
{
    [Fact]
    public async Task TranslateAsync_SendsSignedFormDataAndParsesTranslatedText()
    {
        var handler = new StubHttpMessageHandler(async request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://fanyi-api.baidu.com/api/trans/vip/translate", request.RequestUri?.ToString());

            var form = ParseForm(await request.Content!.ReadAsStringAsync());
            Assert.Equal("APPIDTEST", form["appid"]);
            Assert.Equal("你好世界", form["q"]);
            Assert.Equal("zh", form["from"]);
            Assert.Equal("en", form["to"]);
            Assert.Equal("salt123", form["salt"]);
            Assert.Equal(CreateSign("APPIDTEST", "你好世界", "salt123", "SECRETTEST"), form["sign"]);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                      "from": "zh",
                      "to": "en",
                      "trans_result": [
                        {
                          "src": "你好世界",
                          "dst": "Hello world"
                        }
                      ]
                    }
                    """, Encoding.UTF8, "application/json")
            };
        });

        var provider = new BaiduGeneralTextTranslationProvider(
            new HttpClient(handler),
            new TranslationProviderConfig
            {
                ProviderType = TranslationProviderType.BaiduGeneralTextTranslation,
                SecretId = "APPIDTEST",
                SecretKey = "SECRETTEST",
                TimeoutSeconds = 6
            },
            () => "salt123");

        var translated = await provider.TranslateAsync("你好世界", "en", CancellationToken.None);

        Assert.Equal("Hello world", translated);
    }

    [Fact]
    public async Task TestConnectionAsync_MapsSignatureErrorToReadableError()
    {
        var handler = new StubHttpMessageHandler(_ =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                      "error_code": "54001",
                      "error_msg": "Invalid Sign"
                    }
                    """, Encoding.UTF8, "application/json")
            });
        });

        var config = new TranslationProviderConfig
        {
            ProviderType = TranslationProviderType.BaiduGeneralTextTranslation,
            SecretId = "APPIDTEST",
            SecretKey = "SECRETTEST",
            TimeoutSeconds = 6
        };

        var provider = new BaiduGeneralTextTranslationProvider(new HttpClient(handler), config, () => "salt123");
        var result = await provider.TestConnectionAsync(config, "en", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("authentication_failed", result.ErrorCode);
        Assert.Equal("签名错误，请检查 AppId 和 AppKey。", result.Message);
    }

    [Fact]
    public async Task TestConnectionAsync_FailsFastWhenCredentialsMissing()
    {
        var provider = new BaiduGeneralTextTranslationProvider(
            new HttpClient(new StubHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))),
            new TranslationProviderConfig
            {
                ProviderType = TranslationProviderType.BaiduGeneralTextTranslation,
                SecretId = string.Empty,
                SecretKey = string.Empty,
                TimeoutSeconds = 6
            });

        var result = await provider.TestConnectionAsync(
            new TranslationProviderConfig { ProviderType = TranslationProviderType.BaiduGeneralTextTranslation },
            "en",
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("missing_credentials", result.ErrorCode);
    }

    private static Dictionary<string, string> ParseForm(string content)
    {
        return content
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                pair => Uri.UnescapeDataString(pair[0]),
                pair => Uri.UnescapeDataString((pair.Length > 1 ? pair[1] : string.Empty).Replace("+", " ")));
    }

    private static string CreateSign(string appId, string query, string salt, string secretKey)
    {
        var raw = $"{appId}{query}{salt}{secretKey}";
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request);
        }
    }
}

using System.Net;
using System.Text;
using System.Text.Json;
using TripleSpaceTranslator.Core.Models;
using TripleSpaceTranslator.Core.Services;

namespace TripleSpaceTranslator.Tests;

public sealed class TencentMachineTranslationProviderTests
{
    [Fact]
    public async Task TranslateAsync_SendsTencentHeadersAndParsesTargetText()
    {
        var timestamp = new DateTimeOffset(2026, 4, 26, 12, 0, 0, TimeSpan.Zero);
        var handler = new StubHttpMessageHandler(async request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://tmt.tencentcloudapi.com/", request.RequestUri?.ToString());
            Assert.Equal("TextTranslate", string.Join(",", request.Headers.GetValues("X-TC-Action")));
            Assert.Equal("2018-03-21", string.Join(",", request.Headers.GetValues("X-TC-Version")));
            Assert.Equal("ap-guangzhou", string.Join(",", request.Headers.GetValues("X-TC-Region")));
            Assert.True(request.Headers.TryGetValues("Authorization", out var authorizationValues));
            Assert.StartsWith("TC3-HMAC-SHA256 Credential=AKIDTEST/", authorizationValues!.Single(), StringComparison.Ordinal);

            var payload = await request.Content!.ReadAsStringAsync();
            using var document = JsonDocument.Parse(payload);
            Assert.Equal("你好世界", document.RootElement.GetProperty("SourceText").GetString());
            Assert.Equal("zh", document.RootElement.GetProperty("Source").GetString());
            Assert.Equal("en", document.RootElement.GetProperty("Target").GetString());
            Assert.Equal(1001, document.RootElement.GetProperty("ProjectId").GetInt32());

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                      "Response": {
                        "TargetText": "Hello world"
                      }
                    }
                    """, Encoding.UTF8, "application/json")
            };
        });

        var provider = new TencentMachineTranslationProvider(
            new HttpClient(handler),
            new TranslationProviderConfig
            {
                SecretId = "AKIDTEST",
                SecretKey = "SECRETTEST",
                Region = "ap-guangzhou",
                ProjectId = 1001,
                TimeoutSeconds = 6
            },
            () => timestamp);

        var translated = await provider.TranslateAsync("你好世界", "en", CancellationToken.None);

        Assert.Equal("Hello world", translated);
    }

    [Fact]
    public async Task TestConnectionAsync_MapsAuthFailureToReadableError()
    {
        var handler = new StubHttpMessageHandler(_ =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                      "Response": {
                        "Error": {
                          "Code": "AuthFailure.SignatureFailure",
                          "Message": "The provided credentials could not be validated."
                        }
                      }
                    }
                    """, Encoding.UTF8, "application/json")
            });
        });

        var config = new TranslationProviderConfig
        {
            SecretId = "AKIDTEST",
            SecretKey = "SECRETTEST",
            Region = "ap-guangzhou",
            TimeoutSeconds = 6
        };

        var provider = new TencentMachineTranslationProvider(new HttpClient(handler), config);
        var result = await provider.TestConnectionAsync(config, "en", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("authentication_failed", result.ErrorCode);
        Assert.Equal("The provided credentials could not be validated.", result.Message);
    }

    [Fact]
    public async Task TestConnectionAsync_FailsFastWhenCredentialsMissing()
    {
        var provider = new TencentMachineTranslationProvider(
            new HttpClient(new StubHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))),
            new TranslationProviderConfig
            {
                SecretId = string.Empty,
                SecretKey = string.Empty,
                Region = "ap-guangzhou",
                TimeoutSeconds = 6
            });

        var result = await provider.TestConnectionAsync(new TranslationProviderConfig(), "en", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("missing_credentials", result.ErrorCode);
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

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace WebApplication1.Services;

public sealed class AiProviderService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<AiProviderService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private const string GeminiEndpoint = "https://generativelanguage.googleapis.com/v1beta/models/";
    private const string OpenAiEndpoint = "https://api.openai.com/v1/chat/completions";

    public async Task<AiProviderResult> GenerateAsync(
        string systemPrompt,
        IReadOnlyList<AiProviderMessage> messages,
        CancellationToken cancellationToken)
    {
        var providers = GetProviders();
        if (providers.Count == 0)
        {
            logger.LogWarning("No AI provider is configured. Set Ai:ApiKey and Ai:Model or configure Ai:Providers.");
            return new AiProviderResult(null, AiProviderFailure.NotConfigured);
        }

        var failure = AiProviderFailure.InvalidConfiguration;
        foreach (var provider in providers)
        {
            if (!TryGetEndpoint(provider, out var endpoint))
            {
                logger.LogWarning("Skipping AI provider {ProviderName}: unsupported type or invalid HTTPS endpoint.", provider.Name);
                continue;
            }

            try
            {
                using var providerTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                providerTimeout.CancelAfter(TimeSpan.FromSeconds(15));
                using var request = CreateRequest(provider, endpoint, systemPrompt, messages);
                using var response = await httpClientFactory.CreateClient("ai-provider")
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, providerTimeout.Token);

                if (!response.IsSuccessStatusCode)
                {
                    var errorDetail = await ReadProviderErrorAsync(response, providerTimeout.Token);
                    failure = response.StatusCode switch
                    {
                        System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden => AiProviderFailure.InvalidCredentials,
                        System.Net.HttpStatusCode.NotFound => AiProviderFailure.InvalidModelOrEndpoint,
                        System.Net.HttpStatusCode.TooManyRequests => AiProviderFailure.QuotaExceeded,
                        System.Net.HttpStatusCode.BadRequest => AiProviderFailure.ProviderRejectedRequest,
                        _ when (int)response.StatusCode >= 500 => AiProviderFailure.ProviderUnavailable,
                        _ => AiProviderFailure.ProviderRejectedRequest
                    };
                    logger.LogWarning("AI provider {ProviderName} returned HTTP {StatusCode}: {ProviderError}; trying the next configured provider.",
                        provider.Name, (int)response.StatusCode, errorDetail);
                    continue;
                }

                var content = await ReadCompletionAsync(provider.Type, response, providerTimeout.Token);
                var structuredContent = string.IsNullOrWhiteSpace(content) ? null : NormalizeStructuredChatAnswer(content);
                if (structuredContent is not null)
                {
                    return new AiProviderResult(new AiProviderCompletion(structuredContent, provider.Name, provider.Model), AiProviderFailure.None);
                }

                failure = AiProviderFailure.InvalidResponse;
                logger.LogWarning("AI provider {ProviderName} returned no usable structured answer for model {Model}; trying the next configured provider.", provider.Name, provider.Model);
            }
            catch (HttpRequestException exception)
            {
                failure = AiProviderFailure.ProviderUnavailable;
                logger.LogWarning(exception, "AI provider {ProviderName} could not be reached; trying the next configured provider.", provider.Name);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                failure = AiProviderFailure.ProviderUnavailable;
                logger.LogWarning("AI provider {ProviderName} timed out; trying the next configured provider.", provider.Name);
            }
            catch (JsonException exception)
            {
                failure = AiProviderFailure.InvalidResponse;
                logger.LogWarning(exception, "AI provider {ProviderName} returned an unreadable response; trying the next configured provider.", provider.Name);
            }
        }

        return new AiProviderResult(null, failure);
    }

    private static async Task<string> ReadProviderErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("message", out var message)
                && message.ValueKind == JsonValueKind.String)
            {
                var detail = message.GetString();
                return string.IsNullOrWhiteSpace(detail) ? "No provider error details." : detail[..Math.Min(detail.Length, 500)];
            }
        }
        catch (JsonException)
        {
            return "Provider returned a non-JSON error response.";
        }

        return "No provider error details.";
    }

    private List<AiProviderSettings> GetProviders()
    {
        var providers = configuration.GetSection("Ai:Providers").GetChildren()
            .OrderBy(section => int.TryParse(section.Key, out var index) ? index : int.MaxValue)
            .Select(section => new AiProviderSettings(
                section["Name"] ?? section.Key,
                section["Type"] ?? string.Empty,
                section["ApiKey"] ?? string.Empty,
                section["Model"] ?? string.Empty,
                section["Endpoint"]))
            .Where(provider => !string.IsNullOrWhiteSpace(provider.ApiKey) && !string.IsNullOrWhiteSpace(provider.Model))
            .ToList();

        var legacyApiKey = configuration["Ai:ApiKey"];
        var legacyModel = configuration["Ai:Model"];
        if (!string.IsNullOrWhiteSpace(legacyApiKey) && !string.IsNullOrWhiteSpace(legacyModel) &&
            !providers.Any(provider => provider.Type.Equals("Gemini", StringComparison.OrdinalIgnoreCase) &&
                provider.Model.Equals(legacyModel, StringComparison.Ordinal) &&
                string.Equals(provider.ApiKey, legacyApiKey, StringComparison.Ordinal)))
        {
            providers.Add(new AiProviderSettings("Legacy Gemini", "Gemini", legacyApiKey, legacyModel, null));
        }

        return providers;
    }

    private static bool TryGetEndpoint(AiProviderSettings provider, out Uri endpoint)
    {
        endpoint = new Uri(OpenAiEndpoint);
        var type = provider.Type.Trim();
        if (type.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            endpoint = new Uri($"{GeminiEndpoint}{Uri.EscapeDataString(provider.Model)}:generateContent");
            return true;
        }

        if (type.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("OpenAICompatible", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("Groq", StringComparison.OrdinalIgnoreCase))
        {
            var endpointValue = string.IsNullOrWhiteSpace(provider.Endpoint) ? OpenAiEndpoint : provider.Endpoint;
            if (!Uri.TryCreate(endpointValue, UriKind.Absolute, out var parsedEndpoint)
                || parsedEndpoint is null
                || parsedEndpoint.Scheme != Uri.UriSchemeHttps)
            {
                return false;
            }

            endpoint = parsedEndpoint;
            return true;
        }

        return false;
    }

    private static HttpRequestMessage CreateRequest(
        AiProviderSettings provider,
        Uri endpoint,
        string systemPrompt,
        IReadOnlyList<AiProviderMessage> messages)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        if (provider.Type.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Add("x-goog-api-key", provider.ApiKey);
            request.Content = JsonContent.Create(new
            {
                systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
                contents = messages.Select(message => new
                {
                    role = message.Role == "assistant" ? "model" : "user",
                    parts = new[] { new { text = message.Content } }
                }),
                generationConfig = new
                {
                    temperature = 0.55,
                    maxOutputTokens = 700,
                    responseMimeType = "application/json",
                    responseSchema = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            reply = new { type = "STRING" },
                            recommendedProductCodes = new { type = "ARRAY", items = new { type = "STRING" } }
                        },
                        required = new[] { "reply", "recommendedProductCodes" }
                    }
                }
            }, options: JsonOptions);
            return request;
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
        var payload = new Dictionary<string, object>
        {
            ["model"] = provider.Model,
            ["messages"] = new object[] { new { role = "system", content = systemPrompt } }
                .Concat(messages.Select(message => (object)new { role = message.Role, content = message.Content }))
                .ToArray(),
            ["temperature"] = 0.55,
            ["max_tokens"] = 700
        };
        if (provider.Type.Equals("Groq", StringComparison.OrdinalIgnoreCase))
        {
            payload["include_reasoning"] = false;
        }
        else
        {
            payload["response_format"] = new { type = "json_object" };
        }

        request.Content = JsonContent.Create(payload, options: JsonOptions);
        return request;
    }

    private static async Task<string?> ReadCompletionAsync(
        string providerType,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        if (providerType.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0 &&
                candidates[0].TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var parts))
            {
                return parts.EnumerateArray()
                    .Select(part => part.TryGetProperty("text", out var text) ? text.GetString() : null)
                    .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));
            }

            return null;
        }

        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0 &&
            choices[0].TryGetProperty("message", out var message) && message.TryGetProperty("content", out var answer))
        {
            if (answer.ValueKind == JsonValueKind.String)
            {
                return answer.GetString();
            }

            if (answer.ValueKind == JsonValueKind.Array)
            {
                return string.Concat(answer.EnumerateArray()
                    .Where(part => part.TryGetProperty("type", out var type) && type.GetString() == "text")
                    .Select(part => part.TryGetProperty("text", out var text) ? text.GetString() : null));
            }
        }

        return null;
    }

    private static string? NormalizeStructuredChatAnswer(string content)
    {
        var objectStart = content.IndexOf('{');
        var objectEnd = content.LastIndexOf('}');
        if (objectStart < 0 || objectEnd <= objectStart)
        {
            return null;
        }

        var json = content[objectStart..(objectEnd + 1)];
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object &&
                TryGetPropertyIgnoreCase(root, "reply", out var reply) &&
                reply.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(reply.GetString()) &&
                TryGetPropertyIgnoreCase(root, "recommendedProductCodes", out var codes) &&
                codes.ValueKind == JsonValueKind.Array &&
                codes.EnumerateArray().All(code => code.ValueKind == JsonValueKind.String)
                    ? json
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private sealed record AiProviderSettings(string Name, string Type, string ApiKey, string Model, string? Endpoint);
}

public enum AiProviderFailure
{
    None,
    NotConfigured,
    InvalidConfiguration,
    InvalidCredentials,
    InvalidModelOrEndpoint,
    ProviderRejectedRequest,
    QuotaExceeded,
    ProviderUnavailable,
    InvalidResponse
}

public sealed record AiProviderResult(AiProviderCompletion? Completion, AiProviderFailure Failure);
public sealed record AiProviderMessage(string Role, string Content);
public sealed record AiProviderCompletion(string Content, string ProviderName, string Model);

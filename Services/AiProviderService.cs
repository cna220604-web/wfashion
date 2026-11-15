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

    public async Task<AiProviderCompletion?> GenerateAsync(
        string systemPrompt,
        IReadOnlyList<AiProviderMessage> messages,
        CancellationToken cancellationToken)
    {
        var providers = GetProviders();
        if (providers.Count == 0)
        {
            return null;
        }

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
                    logger.LogWarning("AI provider {ProviderName} returned HTTP {StatusCode}; trying the next configured provider.",
                        provider.Name, (int)response.StatusCode);
                    continue;
                }

                var content = await ReadCompletionAsync(provider.Type, response, providerTimeout.Token);
                if (!string.IsNullOrWhiteSpace(content) && HasStructuredChatAnswer(content))
                {
                    return new AiProviderCompletion(content, provider.Name, provider.Model);
                }

                logger.LogWarning("AI provider {ProviderName} returned no usable structured answer; trying the next configured provider.", provider.Name);
            }
            catch (HttpRequestException exception)
            {
                logger.LogWarning(exception, "AI provider {ProviderName} could not be reached; trying the next configured provider.", provider.Name);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("AI provider {ProviderName} timed out; trying the next configured provider.", provider.Name);
            }
            catch (JsonException exception)
            {
                logger.LogWarning(exception, "AI provider {ProviderName} returned an unreadable response; trying the next configured provider.", provider.Name);
            }
        }

        return null;
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
        endpoint = default!;
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
            return Uri.TryCreate(endpointValue, UriKind.Absolute, out endpoint) && endpoint.Scheme == Uri.UriSchemeHttps;
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

    private static bool HasStructuredChatAnswer(string content)
    {
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        return root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("reply", out var reply) &&
            reply.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(reply.GetString()) &&
            root.TryGetProperty("recommendedProductCodes", out var codes) &&
            codes.ValueKind == JsonValueKind.Array &&
            codes.EnumerateArray().All(code => code.ValueKind == JsonValueKind.String);
    }

    private sealed record AiProviderSettings(string Name, string Type, string ApiKey, string Model, string? Endpoint);
}

public sealed record AiProviderMessage(string Role, string Content);
public sealed record AiProviderCompletion(string Content, string ProviderName, string Model);

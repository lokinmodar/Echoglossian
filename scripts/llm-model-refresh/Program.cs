// <copyright file="Program.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

await LlmModelRefreshRunner.RunAsync(args).ConfigureAwait(false);

/// <summary>
/// Coordinates the standalone model catalog refresh utility.
/// </summary>
internal static class LlmModelRefreshRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly Regex OpenAiMinorVersionPattern = new(
        "^gpt-(\\d+)-(\\d)(-.+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex GeminiMinorVersionPattern = new(
        "^gemini-(\\d+)-(\\d+)(-.+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Runs the utility from command-line arguments.
    /// </summary>
    /// <param name="args">The process arguments.</param>
    /// <returns>A task that completes when the refresh is done.</returns>
    public static async Task RunAsync(string[] args)
    {
        RefreshOptions options = RefreshOptions.Parse(args);
        string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));

        LlmIndexCatalog? llmIndexCatalog = null;
        if (options.ExportLlmIndexSnapshot || options.UsesLlmIndexRemoteCatalog)
        {
            llmIndexCatalog = await GetLlmIndexCatalogAsync(options).ConfigureAwait(false);
        }

        if (options.ExportLlmIndexSnapshot)
        {
            await ExportLlmIndexSnapshotAsync(
                repoRoot,
                options,
                llmIndexCatalog ?? throw new InvalidOperationException("LLM Index catalog was not loaded.")).ConfigureAwait(false);
        }

        foreach (string engine in options.GetOrderedEngines())
        {
            List<TextModelDefinition>? models = await GetModelsForEngineAsync(
                repoRoot,
                options,
                llmIndexCatalog,
                engine).ConfigureAwait(false);

            if (models is null || models.Count == 0)
            {
                Console.WriteLine($"warning: no models collected for {engine}; leaving existing defaults unchanged.");
                continue;
            }

            string filePath = GetDefaultsFilePath(repoRoot, engine);
            string targetNamespace = GetDefaultsNamespace(engine);
            string className = GetDefaultsClassName(engine);
            await WriteDefaultsFileAsync(filePath, targetNamespace, className, models, options.DryRun).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Builds the remote or local model list for a single engine.
    /// </summary>
    /// <param name="repoRoot">The repository root path.</param>
    /// <param name="options">The parsed refresh options.</param>
    /// <param name="catalog">The cached LLM Index catalog when needed.</param>
    /// <param name="engine">The engine name.</param>
    /// <returns>The collected model definitions.</returns>
    private static async Task<List<TextModelDefinition>?> GetModelsForEngineAsync(
        string repoRoot,
        RefreshOptions options,
        LlmIndexCatalog? catalog,
        string engine)
    {
        _ = repoRoot;

        return engine switch
        {
            "OpenAI" => BuildOpenAiDefaults(catalog ?? throw new InvalidOperationException("OpenAI refresh requires the LLM Index catalog.")),
            "Gemini" => BuildGeminiDefaults(catalog ?? throw new InvalidOperationException("Gemini refresh requires the LLM Index catalog.")),
            "DeepSeek" => BuildDeepSeekDefaults(catalog ?? throw new InvalidOperationException("DeepSeek refresh requires the LLM Index catalog.")),
            "OpenRouter" => await BuildOpenRouterDefaultsAsync().ConfigureAwait(false),
            "Ollama" => await BuildOllamaDefaultsAsync(options.OllamaBaseUrl).ConfigureAwait(false),
            "LmStudio" => await BuildLmStudioDefaultsAsync(options.LmStudioBaseUrl, options.LmStudioApiKey).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unsupported engine: {engine}"),
        };
    }

    /// <summary>
    /// Loads the paged LLM Index catalogs required by the utility.
    /// </summary>
    /// <param name="options">The parsed refresh options.</param>
    /// <returns>The populated catalog.</returns>
    private static async Task<LlmIndexCatalog> GetLlmIndexCatalogAsync(RefreshOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.LlmIndexApiKey))
        {
            throw new InvalidOperationException(
                "LLM Index API key is required for remote catalog refresh. Set EGLO_LLMINDEX_API_KEY or pass --llmindex-api-key.");
        }

        List<LlmIndexModel> models = await GetPagedLlmIndexAsync<LlmIndexModel>(
            options.LlmIndexBaseUrl,
            "v1/models",
            options.LlmIndexApiKey).ConfigureAwait(false);
        List<LlmIndexProvider> providers = await GetPagedLlmIndexAsync<LlmIndexProvider>(
            options.LlmIndexBaseUrl,
            "v1/providers",
            options.LlmIndexApiKey).ConfigureAwait(false);

        return new LlmIndexCatalog(models, providers);
    }

    /// <summary>
    /// Fetches every page from a paged LLM Index endpoint.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="baseUrl">The API base URL.</param>
    /// <param name="path">The relative endpoint path.</param>
    /// <param name="apiKey">The bearer API key.</param>
    /// <returns>The flattened item list.</returns>
    private static async Task<List<T>> GetPagedLlmIndexAsync<T>(
        string baseUrl,
        string path,
        string apiKey)
    {
        List<T> results = new();
        int page = 1;
        int totalPages = 1;

        do
        {
            string url = BuildUrl(baseUrl, path, $"page={page}&limit=100");
            using HttpRequestMessage request = new(HttpMethod.Get, url);
            request.Headers.Authorization = new("Bearer", apiKey);

            using HttpResponseMessage response = await HttpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            LlmIndexPage<T>? payload = await JsonSerializer.DeserializeAsync<LlmIndexPage<T>>(stream, JsonOptions).ConfigureAwait(false);
            if (payload?.Data is null)
            {
                break;
            }

            results.AddRange(payload.Data.Where(item => item is not null)!);
            totalPages = payload.TotalPages <= 0 ? 1 : payload.TotalPages;
            page++;
        }
        while (page <= totalPages);

        return results;
    }

    /// <summary>
    /// Exports an advisory JSON snapshot from LLM Index for manual review.
    /// </summary>
    /// <param name="repoRoot">The repository root path.</param>
    /// <param name="options">The parsed options.</param>
    /// <param name="catalog">The loaded catalog.</param>
    /// <returns>A task that completes after writing the snapshot.</returns>
    private static async Task ExportLlmIndexSnapshotAsync(
        string repoRoot,
        RefreshOptions options,
        LlmIndexCatalog catalog)
    {
        string outputPath = ResolveOutputPath(repoRoot, options.LlmIndexSnapshotPath);
        object payload = new
        {
            generatedAtUtc = DateTime.UtcNow.ToString("O"),
            source = "llmindex",
            baseUrl = options.LlmIndexBaseUrl,
            notes = new[]
            {
                "Advisory catalog for manual review.",
                "Remote engine defaults are generated from LLM Index or public endpoints and should still be code-reviewed before commit.",
            },
            providers = catalog.Providers
                .Where(provider => provider.Id is "openai" or "google-ai" or "deepseek" or "openrouter" or "lmstudio")
                .OrderBy(provider => provider.Id, StringComparer.Ordinal)
                .Select(provider => new
                {
                    id = provider.Id,
                    name = provider.Name,
                    type = provider.Type,
                    website = provider.Website,
                    description = provider.Description,
                })
                .ToList(),
            suggestedCandidates = new
            {
                OpenAI = BuildOpenAiDefaults(catalog).Select(ToSnapshotModel).ToList(),
                Gemini = BuildGeminiDefaults(catalog).Select(ToSnapshotModel).ToList(),
                DeepSeek = BuildDeepSeekDefaults(catalog).Select(ToSnapshotModel).ToList(),
            },
        };

        string json = JsonSerializer.Serialize(payload, JsonOptions) + Environment.NewLine;
        if (options.DryRun)
        {
            Console.WriteLine($"[dry-run] would write snapshot {outputPath}");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, json, new UTF8Encoding(false)).ConfigureAwait(false);
        Console.WriteLine($"updated {outputPath}");
    }

    /// <summary>
    /// Builds the OpenAI defaults from the LLM Index catalog.
    /// </summary>
    /// <param name="catalog">The loaded catalog.</param>
    /// <returns>The normalized defaults list.</returns>
    private static List<TextModelDefinition> BuildOpenAiDefaults(LlmIndexCatalog catalog)
    {
        IEnumerable<TextModelDefinition> models = catalog.Models
            .Where(model => string.Equals(model.DeveloperId, "openai", StringComparison.OrdinalIgnoreCase))
            .Where(model => string.Equals(model.Status, "active", StringComparison.OrdinalIgnoreCase))
            .Select(model => NormalizeOpenAiModel(model))
            .Where(model => model is not null)!
            .Cast<TextModelDefinition>();

        List<TextModelDefinition> deduped = DeduplicateById(models);
        SetPreferredDefault(deduped, "gpt-4o-mini", "gpt-4.1-mini", "gpt-4o");
        return deduped;
    }

    /// <summary>
    /// Builds the Gemini defaults from the LLM Index catalog.
    /// </summary>
    /// <param name="catalog">The loaded catalog.</param>
    /// <returns>The normalized defaults list.</returns>
    private static List<TextModelDefinition> BuildGeminiDefaults(LlmIndexCatalog catalog)
    {
        IEnumerable<TextModelDefinition> models = catalog.Models
            .Where(model => string.Equals(model.Status, "active", StringComparison.OrdinalIgnoreCase))
            .Where(model => string.Equals(model.DeveloperId, "google", StringComparison.OrdinalIgnoreCase)
                || string.Equals(model.DeveloperId, "googledeepmind", StringComparison.OrdinalIgnoreCase))
            .Select(model => NormalizeGeminiModel(model))
            .Where(model => model is not null)!
            .Cast<TextModelDefinition>();

        List<TextModelDefinition> deduped = DeduplicateById(models);
        SetPreferredDefault(deduped, "gemini-pro", "gemini-1.5-flash", "gemini-2.5-flash");
        return deduped;
    }

    /// <summary>
    /// Builds the DeepSeek defaults from the LLM Index catalog.
    /// </summary>
    /// <param name="catalog">The loaded catalog.</param>
    /// <returns>The normalized defaults list.</returns>
    private static List<TextModelDefinition> BuildDeepSeekDefaults(LlmIndexCatalog catalog)
    {
        IEnumerable<TextModelDefinition> models = catalog.Models
            .Where(model => string.Equals(model.DeveloperId, "deepseek", StringComparison.OrdinalIgnoreCase))
            .Where(model => string.Equals(model.Status, "active", StringComparison.OrdinalIgnoreCase))
            .Select(model => NormalizeDeepSeekModel(model))
            .Where(model => model is not null)!
            .Cast<TextModelDefinition>();

        List<TextModelDefinition> deduped = DeduplicateById(models);
        SetPreferredDefault(deduped, "deepseek-chat", "deepseek-chat-v3.1", "deepseek-r1");
        return deduped;
    }

    /// <summary>
    /// Builds the OpenRouter defaults from the public OpenRouter model endpoint.
    /// </summary>
    /// <returns>The OpenRouter defaults list.</returns>
    private static async Task<List<TextModelDefinition>> BuildOpenRouterDefaultsAsync()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "https://openrouter.ai/api/v1/models");
        using HttpResponseMessage response = await HttpClient.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        OpenRouterResponse? payload = await JsonSerializer.DeserializeAsync<OpenRouterResponse>(stream, JsonOptions).ConfigureAwait(false);
        IEnumerable<TextModelDefinition> models = (payload?.Data ?? [])
            .Where(model => !string.IsNullOrWhiteSpace(model.Id))
            .Where(IsUsefulOpenRouterModel)
            .Select(model => new TextModelDefinition(
                model.Id!,
                $"🛰 {model.Id}",
                true,
                model.Architecture?.InputModalities?.Contains("image", StringComparer.OrdinalIgnoreCase) == true,
                model.Id!.Contains("turbo", StringComparison.OrdinalIgnoreCase)
                    || model.Id.Contains("flash", StringComparison.OrdinalIgnoreCase)
                    || model.Id.Contains("fast", StringComparison.OrdinalIgnoreCase),
                model.Id.Contains("mini", StringComparison.OrdinalIgnoreCase)
                    || model.Id.Contains("nano", StringComparison.OrdinalIgnoreCase)
                    || model.Id.Contains("lite", StringComparison.OrdinalIgnoreCase),
                false,
                "OpenRouter",
                null))
            .OrderBy(model => model.Id, StringComparer.Ordinal);

        List<TextModelDefinition> deduped = DeduplicateById(models);
        SetPreferredDefault(deduped, "openrouter/auto", "mistral", "openai/gpt-4o-mini");
        return deduped;
    }

    /// <summary>
    /// Builds the Ollama defaults from the local Ollama tags endpoint.
    /// </summary>
    /// <param name="baseUrl">The configured local base URL.</param>
    /// <returns>The Ollama defaults list.</returns>
    private static async Task<List<TextModelDefinition>?> BuildOllamaDefaultsAsync(string baseUrl)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, $"{baseUrl.TrimEnd('/')}/api/tags");
        using HttpResponseMessage response = await HttpClient.SendAsync(request).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        OllamaTagsResponse? payload = await JsonSerializer.DeserializeAsync<OllamaTagsResponse>(stream, JsonOptions).ConfigureAwait(false);
        IEnumerable<TextModelDefinition> models = (payload?.Models ?? [])
            .Where(model => !string.IsNullOrWhiteSpace(model.Name))
            .Select(model =>
            {
                string tier = model.Name!.Contains(':', StringComparison.Ordinal)
                    ? model.Name.Split(':')[0]
                    : string.Empty;
                return new TextModelDefinition(
                    model.Name!,
                    $"🦙 {model.Name}",
                    true,
                    false,
                    false,
                    false,
                    false,
                    "Ollama",
                    string.IsNullOrWhiteSpace(tier) ? null : tier);
            })
            .OrderBy(model => model.Id, StringComparer.Ordinal);

        List<TextModelDefinition> deduped = DeduplicateById(models);
        SetPreferredDefault(deduped, "llama3");
        return deduped;
    }

    /// <summary>
    /// Builds the LM Studio defaults from the local OpenAI-compatible model endpoint.
    /// </summary>
    /// <param name="baseUrl">The configured LM Studio base URL.</param>
    /// <param name="apiKey">The optional LM Studio bearer token.</param>
    /// <returns>The LM Studio defaults list.</returns>
    private static async Task<List<TextModelDefinition>?> BuildLmStudioDefaultsAsync(string baseUrl, string? apiKey)
    {
        string modelsUrl = baseUrl.TrimEnd('/').EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? $"{baseUrl.TrimEnd('/')}/models"
            : $"{baseUrl.TrimEnd('/')}/v1/models";

        using HttpRequestMessage request = new(HttpMethod.Get, modelsUrl);
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Authorization = new("Bearer", apiKey);
        }

        using HttpResponseMessage response = await HttpClient.SendAsync(request).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        OpenAiStyleModelsResponse? payload = await JsonSerializer.DeserializeAsync<OpenAiStyleModelsResponse>(stream, JsonOptions).ConfigureAwait(false);
        IEnumerable<TextModelDefinition> models = (payload?.Data ?? [])
            .Where(model => !string.IsNullOrWhiteSpace(model.Id))
            .Where(model => !model.Id!.Contains("vision", StringComparison.OrdinalIgnoreCase))
            .Select(model => new TextModelDefinition(
                model.Id!,
                $"🧠 {model.Id}",
                true,
                false,
                false,
                false,
                false,
                "LmStudio",
                null))
            .OrderBy(model => model.Id, StringComparer.Ordinal);

        List<TextModelDefinition> deduped = DeduplicateById(models);
        SetPreferredDefault(deduped, "lmstudio/llama3", "llama3");
        return deduped;
    }

    /// <summary>
    /// Determines whether a public OpenRouter model is relevant to Echoglossian text translation use.
    /// </summary>
    /// <param name="model">The candidate model.</param>
    /// <returns><see langword="true"/> when the model should be kept.</returns>
    private static bool IsUsefulOpenRouterModel(OpenRouterModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Id))
        {
            return false;
        }

        if (model.Id.Contains("whisper", StringComparison.OrdinalIgnoreCase)
            || model.Id.Contains("embed", StringComparison.OrdinalIgnoreCase)
            || model.Id.Contains("tts", StringComparison.OrdinalIgnoreCase)
            || model.Id.Contains("moderation", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (model.Architecture?.OutputModalities is { Count: > 0 } outputModalities
            && !outputModalities.Contains("text", StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Normalizes a single OpenAI catalog row into an emitted defaults row.
    /// </summary>
    /// <param name="model">The source model row.</param>
    /// <returns>The normalized row, or <see langword="null"/> when it should be skipped.</returns>
    private static TextModelDefinition? NormalizeOpenAiModel(LlmIndexModel model)
    {
        string? rawId = model.Id?.Trim();
        if (string.IsNullOrWhiteSpace(rawId))
        {
            return null;
        }

        if (rawId.Contains("image", StringComparison.OrdinalIgnoreCase)
            || rawId.Contains("audio", StringComparison.OrdinalIgnoreCase)
            || rawId.Contains("search", StringComparison.OrdinalIgnoreCase)
            || rawId.Contains("realtime", StringComparison.OrdinalIgnoreCase)
            || rawId.Contains("transcribe", StringComparison.OrdinalIgnoreCase)
            || rawId.Contains("tts", StringComparison.OrdinalIgnoreCase)
            || rawId.Contains("whisper", StringComparison.OrdinalIgnoreCase)
            || rawId.Contains("embedding", StringComparison.OrdinalIgnoreCase)
            || rawId.Contains("moderation", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!rawId.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase)
            && !rawId.StartsWith("chatgpt-", StringComparison.OrdinalIgnoreCase)
            && !rawId.StartsWith("o1", StringComparison.OrdinalIgnoreCase)
            && !rawId.StartsWith("o3", StringComparison.OrdinalIgnoreCase)
            && !rawId.StartsWith("o4", StringComparison.OrdinalIgnoreCase)
            && !rawId.StartsWith("codex-", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string normalizedId = NormalizeOpenAiId(rawId);
        bool supportsVision = normalizedId.Contains("gpt-4o", StringComparison.OrdinalIgnoreCase)
            || normalizedId.StartsWith("chatgpt-4o", StringComparison.OrdinalIgnoreCase)
            || normalizedId.StartsWith("gpt-4.1", StringComparison.OrdinalIgnoreCase);

        return new TextModelDefinition(
            normalizedId,
            GetOpenAiDisplayName(normalizedId),
            true,
            supportsVision,
            normalizedId.Contains("turbo", StringComparison.OrdinalIgnoreCase),
            normalizedId.Contains("mini", StringComparison.OrdinalIgnoreCase)
                || normalizedId.Contains("nano", StringComparison.OrdinalIgnoreCase),
            false,
            "OpenAI",
            null);
    }

    /// <summary>
    /// Normalizes a single Gemini catalog row into an emitted defaults row.
    /// </summary>
    /// <param name="model">The source model row.</param>
    /// <returns>The normalized row, or <see langword="null"/> when it should be skipped.</returns>
    private static TextModelDefinition? NormalizeGeminiModel(LlmIndexModel model)
    {
        string? rawId = model.Id?.Trim();
        if (string.IsNullOrWhiteSpace(rawId) || !rawId.StartsWith("gemini", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (rawId.Contains("image", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string normalizedId = NormalizeGeminiId(rawId);
        return new TextModelDefinition(
            normalizedId,
            GetGeminiDisplayName(normalizedId),
            true,
            false,
            normalizedId.Contains("flash", StringComparison.OrdinalIgnoreCase)
                || normalizedId.Contains("pro", StringComparison.OrdinalIgnoreCase),
            normalizedId.Contains("flash", StringComparison.OrdinalIgnoreCase)
                || normalizedId.Contains("lite", StringComparison.OrdinalIgnoreCase),
            false,
            "Gemini",
            null);
    }

    /// <summary>
    /// Normalizes a single DeepSeek catalog row into an emitted defaults row.
    /// </summary>
    /// <param name="model">The source model row.</param>
    /// <returns>The normalized row, or <see langword="null"/> when it should be skipped.</returns>
    private static TextModelDefinition? NormalizeDeepSeekModel(LlmIndexModel model)
    {
        string? rawId = model.Id?.Trim();
        if (string.IsNullOrWhiteSpace(rawId) || !rawId.StartsWith("deepseek-", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (rawId.Contains("distill", StringComparison.OrdinalIgnoreCase)
            || rawId.Contains("qwen", StringComparison.OrdinalIgnoreCase)
            || rawId.Contains("llama", StringComparison.OrdinalIgnoreCase)
            || rawId.Contains("vl", StringComparison.OrdinalIgnoreCase)
            || rawId.Contains("prover", StringComparison.OrdinalIgnoreCase)
            || rawId.Contains("free", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new TextModelDefinition(
            rawId,
            GetDeepSeekDisplayName(rawId),
            true,
            false,
            rawId.Contains("turbo", StringComparison.OrdinalIgnoreCase)
                || rawId.Contains("flash", StringComparison.OrdinalIgnoreCase),
            rawId.Contains("mini", StringComparison.OrdinalIgnoreCase),
            false,
            "DeepSeek",
            null);
    }

    /// <summary>
    /// Applies the known OpenAI id normalization rules for LLM Index rows.
    /// </summary>
    /// <param name="rawId">The raw catalog id.</param>
    /// <returns>The normalized id.</returns>
    private static string NormalizeOpenAiId(string rawId)
    {
        Match match = OpenAiMinorVersionPattern.Match(rawId);
        if (!match.Success)
        {
            return rawId;
        }

        return $"gpt-{match.Groups[1].Value}.{match.Groups[2].Value}{match.Groups[3].Value}";
    }

    /// <summary>
    /// Applies the known Gemini id normalization rules for LLM Index rows.
    /// </summary>
    /// <param name="rawId">The raw catalog id.</param>
    /// <returns>The normalized id.</returns>
    private static string NormalizeGeminiId(string rawId)
    {
        Match match = GeminiMinorVersionPattern.Match(rawId);
        if (!match.Success)
        {
            return rawId;
        }

        return $"gemini-{match.Groups[1].Value}.{match.Groups[2].Value}{match.Groups[3].Value}";
    }

    /// <summary>
    /// Builds the display name for an OpenAI family id.
    /// </summary>
    /// <param name="id">The normalized id.</param>
    /// <returns>The display label.</returns>
    private static string GetOpenAiDisplayName(string id)
    {
        if (id.StartsWith("gpt-4o", StringComparison.OrdinalIgnoreCase)
            || id.StartsWith("chatgpt-4o", StringComparison.OrdinalIgnoreCase))
        {
            return $"👁 {id}";
        }

        if (id.StartsWith("gpt-4", StringComparison.OrdinalIgnoreCase)
            || id.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase))
        {
            return $"🧠 {id}";
        }

        if (id.StartsWith("gpt-3.5", StringComparison.OrdinalIgnoreCase))
        {
            return $"⚡ {id}";
        }

        if (id.StartsWith("o", StringComparison.OrdinalIgnoreCase))
        {
            return id.Contains("mini", StringComparison.OrdinalIgnoreCase)
                || id.Contains("nano", StringComparison.OrdinalIgnoreCase)
                ? $"🔹 {id}"
                : $"🔷 {id}";
        }

        return $"🧩 {id}";
    }

    /// <summary>
    /// Builds the display name for a Gemini family id.
    /// </summary>
    /// <param name="id">The normalized id.</param>
    /// <returns>The display label.</returns>
    private static string GetGeminiDisplayName(string id)
    {
        if (string.Equals(id, "gemini-pro", StringComparison.OrdinalIgnoreCase))
        {
            return $"🔷 {id}";
        }

        if (id.Contains("flash", StringComparison.OrdinalIgnoreCase)
            || id.Contains("lite", StringComparison.OrdinalIgnoreCase))
        {
            return $"⚡ {id}";
        }

        if (id.Contains("pro", StringComparison.OrdinalIgnoreCase))
        {
            return $"🟢 {id}";
        }

        return $"🧩 {id}";
    }

    /// <summary>
    /// Builds the display name for a DeepSeek family id.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <returns>The display label.</returns>
    private static string GetDeepSeekDisplayName(string id)
    {
        if (id.StartsWith("deepseek-chat", StringComparison.OrdinalIgnoreCase))
        {
            return $"💬 {id}";
        }

        if (id.StartsWith("deepseek-r1", StringComparison.OrdinalIgnoreCase)
            || id.Contains("reasoner", StringComparison.OrdinalIgnoreCase))
        {
            return $"🧠 {id}";
        }

        return $"🧩 {id}";
    }

    /// <summary>
    /// Deduplicates and sorts models by id.
    /// </summary>
    /// <param name="models">The candidate models.</param>
    /// <returns>The deduplicated list.</returns>
    private static List<TextModelDefinition> DeduplicateById(IEnumerable<TextModelDefinition> models)
    {
        Dictionary<string, TextModelDefinition> byId = new(StringComparer.Ordinal);
        foreach (TextModelDefinition model in models)
        {
            byId.TryAdd(model.Id, model);
        }

        return byId.Values
            .OrderBy(model => model.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Marks the preferred default row when one of the preferred ids is present.
    /// </summary>
    /// <param name="models">The mutable model list.</param>
    /// <param name="preferredIds">The preferred ids in order.</param>
    private static void SetPreferredDefault(List<TextModelDefinition> models, params string[] preferredIds)
    {
        if (models.Count == 0)
        {
            return;
        }

        for (int i = 0; i < models.Count; i++)
        {
            models[i] = models[i] with { IsDefault = false };
        }

        foreach (string preferredId in preferredIds)
        {
            int index = models.FindIndex(model => string.Equals(model.Id, preferredId, StringComparison.Ordinal));
            if (index >= 0)
            {
                models[index] = models[index] with { IsDefault = true };
                return;
            }
        }

        models[0] = models[0] with { IsDefault = true };
    }

    /// <summary>
    /// Writes one defaults file in the repository format.
    /// </summary>
    /// <param name="filePath">The target file path.</param>
    /// <param name="targetNamespace">The target namespace.</param>
    /// <param name="className">The target class name.</param>
    /// <param name="models">The source models.</param>
    /// <param name="dryRun">Whether to skip writing.</param>
    /// <returns>A task that completes after comparing or writing the file.</returns>
    private static async Task WriteDefaultsFileAsync(
        string filePath,
        string targetNamespace,
        string className,
        List<TextModelDefinition> models,
        bool dryRun)
    {
        StringBuilder builder = new();
        builder.AppendLine($"// <copyright file=\"{className}.cs\" company=\"lokinmodar\">");
        builder.AppendLine("// Copyright (c) lokinmodar. All rights reserved.");
        builder.AppendLine("// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.");
        builder.AppendLine("// </copyright>");
        builder.AppendLine();

        if (!string.Equals(targetNamespace, "Echoglossian.Translators.OpenAI", StringComparison.Ordinal))
        {
            builder.AppendLine("using Echoglossian.Translators.OpenAI;");
            builder.AppendLine();
        }

        builder.AppendLine($"namespace {targetNamespace};");
        builder.AppendLine();
        builder.AppendLine($"public static class {className}");
        builder.AppendLine("{");
        builder.AppendLine("    public static readonly List<LlmTextModel> PredefinedModels = new()");
        builder.AppendLine("    {");

        for (int i = 0; i < models.Count; i++)
        {
            TextModelDefinition model = models[i];
            string suffix = i < models.Count - 1 ? "," : string.Empty;
            builder.AppendLine("        new LlmTextModel(");
            builder.AppendLine($"            \"{EscapeString(model.Id)}\",");
            builder.AppendLine($"            \"{EscapeString(model.DisplayName)}\",");
            builder.AppendLine($"            {ToLowerInvariant(model.SupportsText)},");
            builder.AppendLine($"            {ToLowerInvariant(model.SupportsVision)},");
            builder.AppendLine($"            {ToLowerInvariant(model.IsTurbo)},");
            builder.AppendLine($"            {ToLowerInvariant(model.IsMini)},");
            builder.AppendLine($"            {ToLowerInvariant(model.IsDefault)},");
            builder.AppendLine($"            \"{EscapeString(model.EngineName)}\"" + (string.IsNullOrWhiteSpace(model.TierOverride) ? string.Empty : ","));
            if (!string.IsNullOrWhiteSpace(model.TierOverride))
            {
                builder.AppendLine($"            \"{EscapeString(model.TierOverride!)}\"");
            }

            builder.AppendLine($"        ){suffix}");
        }

        builder.AppendLine("    };");
        builder.AppendLine("}");

        string content = builder.ToString();
        if (dryRun)
        {
            Console.WriteLine($"[dry-run] would write {filePath} with {models.Count} models");
            return;
        }

        string existing = File.Exists(filePath)
            ? await File.ReadAllTextAsync(filePath).ConfigureAwait(false)
            : string.Empty;
        if (string.Equals(existing, content, StringComparison.Ordinal))
        {
            Console.WriteLine($"no change for {filePath}");
            return;
        }

        await File.WriteAllTextAsync(filePath, content, new UTF8Encoding(false)).ConfigureAwait(false);
        Console.WriteLine($"updated {filePath} ({models.Count} models)");
    }

    /// <summary>
    /// Escapes a string for C# source emission.
    /// </summary>
    /// <param name="value">The value to escape.</param>
    /// <returns>The escaped source literal contents.</returns>
    private static string EscapeString(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    /// <summary>
    /// Converts a boolean to the lower-case C# literal.
    /// </summary>
    /// <param name="value">The boolean value.</param>
    /// <returns>The emitted literal.</returns>
    private static string ToLowerInvariant(bool value)
    {
        return value ? "true" : "false";
    }

    /// <summary>
    /// Gets the target defaults file path for an engine.
    /// </summary>
    /// <param name="repoRoot">The repository root.</param>
    /// <param name="engine">The engine name.</param>
    /// <returns>The file path.</returns>
    private static string GetDefaultsFilePath(string repoRoot, string engine)
    {
        return engine switch
        {
            "OpenAI" => Path.Combine(repoRoot, "Translators", "OpenAI", "OpenAITextModelDefaults.cs"),
            "OpenRouter" => Path.Combine(repoRoot, "Translators", "OpenRouter", "OpenRouterTextModelDefaults.cs"),
            "Gemini" => Path.Combine(repoRoot, "Translators", "Gemini", "GeminiTextModelDefaults.cs"),
            "DeepSeek" => Path.Combine(repoRoot, "Translators", "DeepSeek", "DeepSeekTextModelDefaults.cs"),
            "Ollama" => Path.Combine(repoRoot, "Translators", "Ollama", "OllamaTextModelDefaults.cs"),
            "LmStudio" => Path.Combine(repoRoot, "Translators", "LmStudio", "LmStudioTextModelDefaults.cs"),
            _ => throw new InvalidOperationException($"Unsupported engine: {engine}"),
        };
    }

    /// <summary>
    /// Gets the defaults namespace for an engine.
    /// </summary>
    /// <param name="engine">The engine name.</param>
    /// <returns>The namespace.</returns>
    private static string GetDefaultsNamespace(string engine)
    {
        return engine switch
        {
            "OpenAI" => "Echoglossian.Translators.OpenAI",
            "OpenRouter" => "Echoglossian.Translators.OpenRouter",
            "Gemini" => "Echoglossian.Translators.Gemini",
            "DeepSeek" => "Echoglossian.Translators.DeepSeek",
            "Ollama" => "Echoglossian.Translators.Ollama",
            "LmStudio" => "Echoglossian.Translators.LmStudio",
            _ => throw new InvalidOperationException($"Unsupported engine: {engine}"),
        };
    }

    /// <summary>
    /// Gets the defaults class name for an engine.
    /// </summary>
    /// <param name="engine">The engine name.</param>
    /// <returns>The class name.</returns>
    private static string GetDefaultsClassName(string engine)
    {
        return engine switch
        {
            "OpenAI" => "OpenAITextModelDefaults",
            "OpenRouter" => "OpenRouterTextModelDefaults",
            "Gemini" => "GeminiTextModelDefaults",
            "DeepSeek" => "DeepSeekTextModelDefaults",
            "Ollama" => "OllamaTextModelDefaults",
            "LmStudio" => "LmStudioTextModelDefaults",
            _ => throw new InvalidOperationException($"Unsupported engine: {engine}"),
        };
    }

    /// <summary>
    /// Resolves the advisory snapshot path.
    /// </summary>
    /// <param name="repoRoot">The repository root.</param>
    /// <param name="configuredPath">The configured path.</param>
    /// <returns>The absolute output path.</returns>
    private static string ResolveOutputPath(string repoRoot, string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.Combine(repoRoot, "artifacts", "llmindex-model-catalog.json");
        }

        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(repoRoot, configuredPath);
    }

    /// <summary>
    /// Builds a URL from a base URL, relative path, and optional query string.
    /// </summary>
    /// <param name="baseUrl">The base URL.</param>
    /// <param name="path">The relative path.</param>
    /// <param name="query">The optional query string.</param>
    /// <returns>The combined URL.</returns>
    private static string BuildUrl(string baseUrl, string path, string? query = null)
    {
        string url = $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
        return string.IsNullOrWhiteSpace(query) ? url : $"{url}?{query}";
    }

    /// <summary>
    /// Creates the shared HTTP client.
    /// </summary>
    /// <returns>The configured client.</returns>
    private static HttpClient CreateHttpClient()
    {
        HttpClient client = new();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Echoglossian-LlmModelRefresh/1.0");
        return client;
    }

    /// <summary>
    /// Converts a defaults row into a small advisory snapshot row.
    /// </summary>
    /// <param name="model">The defaults row.</param>
    /// <returns>The snapshot row.</returns>
    private static object ToSnapshotModel(TextModelDefinition model)
    {
        return new
        {
            id = model.Id,
            displayName = model.DisplayName,
            isDefault = model.IsDefault,
        };
    }
}

/// <summary>
/// Represents the command-line options for the refresh utility.
/// </summary>
internal sealed class RefreshOptions
{
    private static readonly string[] DefaultRemoteEngines = ["OpenAI", "OpenRouter", "Gemini", "DeepSeek"];

    /// <summary>
    /// Gets the selected engine names.
    /// </summary>
    public List<string> Engines { get; init; } = [.. DefaultRemoteEngines];

    /// <summary>
    /// Gets a value indicating whether local engines should be included.
    /// </summary>
    public bool IncludeLocal { get; init; }

    /// <summary>
    /// Gets a value indicating whether files should be left untouched.
    /// </summary>
    public bool DryRun { get; init; }

    /// <summary>
    /// Gets a value indicating whether an LLM Index advisory snapshot should be written.
    /// </summary>
    public bool ExportLlmIndexSnapshot { get; init; }

    /// <summary>
    /// Gets the optional LLM Index snapshot output path.
    /// </summary>
    public string? LlmIndexSnapshotPath { get; init; }

    /// <summary>
    /// Gets the LLM Index bearer API key.
    /// </summary>
    public string LlmIndexApiKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the LLM Index base URL.
    /// </summary>
    public string LlmIndexBaseUrl { get; init; } = "https://api.llmindex.net";

    /// <summary>
    /// Gets the local Ollama base URL.
    /// </summary>
    public string OllamaBaseUrl { get; init; } = "http://localhost:11434";

    /// <summary>
    /// Gets the local LM Studio base URL.
    /// </summary>
    public string LmStudioBaseUrl { get; init; } = "http://localhost:1234/v1";

    /// <summary>
    /// Gets the optional local LM Studio bearer token.
    /// </summary>
    public string? LmStudioApiKey { get; init; }

    /// <summary>
    /// Gets a value indicating whether any remote engine uses the LLM Index catalog.
    /// </summary>
    public bool UsesLlmIndexRemoteCatalog =>
        this.GetOrderedEngines().Any(engine => engine is "OpenAI" or "Gemini" or "DeepSeek");

    /// <summary>
    /// Parses the command-line arguments and environment variables.
    /// </summary>
    /// <param name="args">The process arguments.</param>
    /// <returns>The parsed options.</returns>
    public static RefreshOptions Parse(string[] args)
    {
        RefreshOptions options = new();
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--engines":
                    options = options with
                    {
                        Engines = ParseEngines(GetRequiredValue(args, ref i, "--engines")),
                    };
                    break;
                case "--include-local":
                    options = options with { IncludeLocal = true };
                    break;
                case "--dry-run":
                    options = options with { DryRun = true };
                    break;
                case "--export-llmindex-snapshot":
                    options = options with { ExportLlmIndexSnapshot = true };
                    break;
                case "--llmindex-snapshot-path":
                    options = options with { LlmIndexSnapshotPath = GetRequiredValue(args, ref i, "--llmindex-snapshot-path") };
                    break;
                case "--llmindex-api-key":
                    options = options with { LlmIndexApiKey = GetRequiredValue(args, ref i, "--llmindex-api-key") };
                    break;
                case "--llmindex-base-url":
                    options = options with { LlmIndexBaseUrl = GetRequiredValue(args, ref i, "--llmindex-base-url") };
                    break;
                case "--ollama-base-url":
                    options = options with { OllamaBaseUrl = GetRequiredValue(args, ref i, "--ollama-base-url") };
                    break;
                case "--lmstudio-base-url":
                    options = options with { LmStudioBaseUrl = GetRequiredValue(args, ref i, "--lmstudio-base-url") };
                    break;
                case "--lmstudio-api-key":
                    options = options with { LmStudioApiKey = GetRequiredValue(args, ref i, "--lmstudio-api-key") };
                    break;
                case "--help":
                case "-h":
                    WriteUsage();
                    Environment.Exit(0);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown argument: {arg}");
            }
        }

        return options with
        {
            LlmIndexApiKey = ResolveSetting(options.LlmIndexApiKey, "EGLO_LLMINDEX_API_KEY", "LLMINDEX_API_KEY"),
            LlmIndexBaseUrl = ResolveSetting(options.LlmIndexBaseUrl, "EGLO_LLMINDEX_BASE_URL"),
            OllamaBaseUrl = ResolveSetting(options.OllamaBaseUrl, "EGLO_OLLAMA_BASE_URL"),
            LmStudioBaseUrl = ResolveSetting(options.LmStudioBaseUrl, "EGLO_LMSTUDIO_BASE_URL"),
            LmStudioApiKey = ResolveSetting(options.LmStudioApiKey, "EGLO_LMSTUDIO_API_KEY"),
        };
    }

    /// <summary>
    /// Returns the ordered and deduplicated engine list.
    /// </summary>
    /// <returns>The selected engine list.</returns>
    public List<string> GetOrderedEngines()
    {
        List<string> engines = this.Engines
            .Where(engine => !string.IsNullOrWhiteSpace(engine))
            .Select(engine => engine.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (this.IncludeLocal)
        {
            if (!engines.Contains("Ollama", StringComparer.Ordinal))
            {
                engines.Add("Ollama");
            }

            if (!engines.Contains("LmStudio", StringComparer.Ordinal))
            {
                engines.Add("LmStudio");
            }
        }

        return engines;
    }

    /// <summary>
    /// Writes the command-line usage.
    /// </summary>
    private static void WriteUsage()
    {
        Console.WriteLine("Usage: dotnet run --project scripts/llm-model-refresh -- [options]");
        Console.WriteLine("  --engines OpenAI,OpenRouter,Gemini,DeepSeek");
        Console.WriteLine("  --include-local");
        Console.WriteLine("  --dry-run");
        Console.WriteLine("  --export-llmindex-snapshot");
        Console.WriteLine("  --llmindex-snapshot-path <path>");
        Console.WriteLine("  --llmindex-api-key <key>");
        Console.WriteLine("  --llmindex-base-url <url>");
        Console.WriteLine("  --ollama-base-url <url>");
        Console.WriteLine("  --lmstudio-base-url <url>");
        Console.WriteLine("  --lmstudio-api-key <key>");
    }

    /// <summary>
    /// Gets a required CLI value after a named option.
    /// </summary>
    /// <param name="args">The argument array.</param>
    /// <param name="index">The current index, which will be advanced.</param>
    /// <param name="optionName">The option name.</param>
    /// <returns>The required value.</returns>
    private static string GetRequiredValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
        {
            throw new InvalidOperationException($"Missing value for {optionName}");
        }

        index++;
        return args[index];
    }

    /// <summary>
    /// Parses the comma-separated engine list.
    /// </summary>
    /// <param name="value">The raw engine argument.</param>
    /// <returns>The parsed list.</returns>
    private static List<string> ParseEngines(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    /// <summary>
    /// Resolves a setting from the explicit value or environment variables.
    /// </summary>
    /// <param name="explicitValue">The explicit CLI value.</param>
    /// <param name="environmentNames">The candidate environment variable names.</param>
    /// <returns>The resolved value.</returns>
    private static string ResolveSetting(string? explicitValue, params string[] environmentNames)
    {
        if (!string.IsNullOrWhiteSpace(explicitValue))
        {
            return explicitValue;
        }

        foreach (string environmentName in environmentNames)
        {
            string? value = Environment.GetEnvironmentVariable(environmentName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }
}

/// <summary>
/// Represents one emitted text model row.
/// </summary>
/// <param name="Id">The canonical model id.</param>
/// <param name="DisplayName">The display name used in the UI.</param>
/// <param name="SupportsText">Whether text is supported.</param>
/// <param name="SupportsVision">Whether vision is supported.</param>
/// <param name="IsTurbo">Whether the model belongs to a turbo or fast family.</param>
/// <param name="IsMini">Whether the model belongs to a mini or lite family.</param>
/// <param name="IsDefault">Whether the row is the preferred default.</param>
/// <param name="EngineName">The engine name.</param>
/// <param name="TierOverride">The optional tier override.</param>
internal sealed record TextModelDefinition(
    string Id,
    string DisplayName,
    bool SupportsText,
    bool SupportsVision,
    bool IsTurbo,
    bool IsMini,
    bool IsDefault,
    string EngineName,
    string? TierOverride);

/// <summary>
/// Holds the loaded LLM Index catalogs.
/// </summary>
/// <param name="Models">The loaded model rows.</param>
/// <param name="Providers">The loaded provider rows.</param>
internal sealed record LlmIndexCatalog(
    List<LlmIndexModel> Models,
    List<LlmIndexProvider> Providers);

/// <summary>
/// Represents one paged LLM Index response.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
internal sealed class LlmIndexPage<T>
{
    /// <summary>
    /// Gets or sets the data rows.
    /// </summary>
    [JsonPropertyName("data")]
    public List<T>? Data { get; set; }

    /// <summary>
    /// Gets or sets the total page count.
    /// </summary>
    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }
}

/// <summary>
/// Represents one LLM Index model row.
/// </summary>
internal sealed class LlmIndexModel
{
    /// <summary>
    /// Gets or sets the model id.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the developer id.
    /// </summary>
    [JsonPropertyName("developerId")]
    public string? DeveloperId { get; set; }

    /// <summary>
    /// Gets or sets the developer name.
    /// </summary>
    [JsonPropertyName("developer")]
    public string? Developer { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }
}

/// <summary>
/// Represents one LLM Index provider row.
/// </summary>
internal sealed class LlmIndexProvider
{
    /// <summary>
    /// Gets or sets the provider id.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the provider type.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the provider website.
    /// </summary>
    [JsonPropertyName("website")]
    public string? Website { get; set; }

    /// <summary>
    /// Gets or sets the provider description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>
/// Represents the OpenRouter public models response.
/// </summary>
internal sealed class OpenRouterResponse
{
    /// <summary>
    /// Gets or sets the model rows.
    /// </summary>
    [JsonPropertyName("data")]
    public List<OpenRouterModel>? Data { get; set; }
}

/// <summary>
/// Represents one OpenRouter public model row.
/// </summary>
internal sealed class OpenRouterModel
{
    /// <summary>
    /// Gets or sets the model id.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the architecture row.
    /// </summary>
    [JsonPropertyName("architecture")]
    public OpenRouterArchitecture? Architecture { get; set; }
}

/// <summary>
/// Represents the OpenRouter architecture row.
/// </summary>
internal sealed class OpenRouterArchitecture
{
    /// <summary>
    /// Gets or sets the input modalities.
    /// </summary>
    [JsonPropertyName("input_modalities")]
    public List<string>? InputModalities { get; set; }

    /// <summary>
    /// Gets or sets the output modalities.
    /// </summary>
    [JsonPropertyName("output_modalities")]
    public List<string>? OutputModalities { get; set; }
}

/// <summary>
/// Represents the local Ollama tags response.
/// </summary>
internal sealed class OllamaTagsResponse
{
    /// <summary>
    /// Gets or sets the model rows.
    /// </summary>
    [JsonPropertyName("models")]
    public List<OllamaModel>? Models { get; set; }
}

/// <summary>
/// Represents one local Ollama model row.
/// </summary>
internal sealed class OllamaModel
{
    /// <summary>
    /// Gets or sets the model name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

/// <summary>
/// Represents an OpenAI-compatible models response.
/// </summary>
internal sealed class OpenAiStyleModelsResponse
{
    /// <summary>
    /// Gets or sets the model rows.
    /// </summary>
    [JsonPropertyName("data")]
    public List<OpenAiStyleModel>? Data { get; set; }
}

/// <summary>
/// Represents one OpenAI-compatible model row.
/// </summary>
internal sealed class OpenAiStyleModel
{
    /// <summary>
    /// Gets or sets the model id.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }
}

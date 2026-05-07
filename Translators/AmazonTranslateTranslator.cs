// <copyright file="AwsTranslateTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Amazon;
using Amazon.Runtime;
using Amazon.Translate;
using Amazon.Translate.Model;

namespace Echoglossian.Translators;

public class AmazonTranslateTranslator : ITranslator
{
    private readonly IPluginLog pluginLog;
    private readonly AmazonTranslateClient translateClient;

    public AmazonTranslateTranslator(IPluginLog pluginLog, Config config)
    {
        this.pluginLog = pluginLog;

        try
        {
            var region =
                RegionEndpoint.GetBySystemName(config.AwsRegion ?? "us-east-1");
            var credentials = ResolveDesktopCredentials(config);

            this.translateClient = new AmazonTranslateClient(
                credentials,
                new AmazonTranslateConfig
                {
                    RegionEndpoint = region,
                });
        }
        catch (Exception ex)
        {
            PluginRuntimeLog.Error(
                this.pluginLog,
                $"Failed to initialize AWS Translate client: {ex}");
            throw;
        }
    }

    /// <summary>
    ///     Resolves AWS credentials only from explicit desktop-safe sources and
    ///     never probes EC2 instance metadata during plugin runtime.
    /// </summary>
    /// <param name="config">The active plugin configuration.</param>
    /// <returns>The resolved AWS credentials.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when no desktop-safe AWS credentials source is available.
    /// </exception>
    private static AWSCredentials ResolveDesktopCredentials(Config config)
    {
        if (!string.IsNullOrWhiteSpace(config.AwsAccessKey) &&
            !string.IsNullOrWhiteSpace(config.AwsSecretKey))
        {
            return string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AWS_SESSION_TOKEN"))
                ? new BasicAWSCredentials(
                    config.AwsAccessKey,
                    config.AwsSecretKey)
                : new SessionAWSCredentials(
                    config.AwsAccessKey,
                    config.AwsSecretKey,
                    Environment.GetEnvironmentVariable("AWS_SESSION_TOKEN"));
        }

        var environmentAccessKey =
            Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
        var environmentSecretKey =
            Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
        if (!string.IsNullOrWhiteSpace(environmentAccessKey) &&
            !string.IsNullOrWhiteSpace(environmentSecretKey))
        {
            var environmentSessionToken =
                Environment.GetEnvironmentVariable("AWS_SESSION_TOKEN");
            return string.IsNullOrWhiteSpace(environmentSessionToken)
                ? new BasicAWSCredentials(
                    environmentAccessKey,
                    environmentSecretKey)
                : new SessionAWSCredentials(
                    environmentAccessKey,
                    environmentSecretKey,
                    environmentSessionToken);
        }

        throw new InvalidOperationException(
            "Amazon Translate requires explicit AWS credentials in Echoglossian or the AWS_ACCESS_KEY_ID/AWS_SECRET_ACCESS_KEY environment variables. Echoglossian will not probe EC2 instance metadata.");
    }

    /// <summary>
    ///     Synchronously translates the given text from source language to target
    ///     language using AWS Translate.
    /// </summary>
    /// <param name="text"></param>
    /// <param name="sourceLanguage"></param>
    /// <param name="targetLanguage"></param>
    /// <returns></returns>
    public string Translate(
        string text,
        string sourceLanguage,
        string targetLanguage)
    {
        PluginRuntimeLog.Debug(this.pluginLog, "AWS Translate sync translate requested.");
        return this.TranslateAsync(text, sourceLanguage, targetLanguage).Result ?? string.Empty;
    }

    /// <summary>
    ///     Asynchronously translates the given text from source language to target
    ///     language using AWS Translate.
    /// </summary>
    /// <param name="text"></param>
    /// <param name="sourceLanguage"></param>
    /// <param name="targetLanguage"></param>
    /// <returns></returns>
    public async Task<string?> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var fixedText = FixText(text);
        PluginRuntimeLog.Debug(this.pluginLog, $"AWS Translate input: {fixedText}");

        try
        {
            var request = new TranslateTextRequest
            {
                Text = fixedText,
                SourceLanguageCode = sourceLanguage,
                TargetLanguageCode = targetLanguage,
            };

            var response =
                await this.translateClient.TranslateTextAsync(request);
            var cleaned = FixText(response.TranslatedText);
            PluginRuntimeLog.Debug(this.pluginLog, $"AWS Translate result: {cleaned}");
            return cleaned;
        }
        catch (Exception ex)
        {
            PluginRuntimeLog.Error(this.pluginLog, $"AWS Translate error: {ex}");
            return string.Empty;
        }
    }
}

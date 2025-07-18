// <copyright file="AwsTranslateTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.Credentials;
using Amazon.Translate;
using Amazon.Translate.Model;

namespace Echoglossian.Translators;

public class AmazonTranslateTranslator : ITranslator
{
    private readonly Config config;
    private readonly IPluginLog pluginLog;
    private readonly AmazonTranslateClient translateClient;

    public AmazonTranslateTranslator(IPluginLog pluginLog, Config config)
    {
        this.pluginLog = pluginLog;
        this.config = config;

        try
        {
            var region =
                RegionEndpoint.GetBySystemName(config.AwsRegion ?? "us-east-1");

            AWSCredentials credentials;

            if (!string.IsNullOrWhiteSpace(config.AwsAccessKey) &&
                !string.IsNullOrWhiteSpace(config.AwsSecretKey))
            {
                credentials = new BasicAWSCredentials(
                    config.AwsAccessKey,
                    config.AwsSecretKey);
            }
            else
            {
                pluginLog.Warning(
                    "Using default AWS credentials provider chain.");
                credentials =
                    DefaultAWSCredentialsIdentityResolver.GetCredentials();
            }

            this.translateClient = new AmazonTranslateClient(
                credentials,
                new AmazonTranslateConfig
                {
                    RegionEndpoint = region,
                });
        }
        catch (Exception ex)
        {
            this.pluginLog.Error(
                $"Failed to initialize AWS Translate client: {ex}");
            throw;
        }
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
        this.pluginLog.Debug("AWS Translate sync translate requested.");
        return this.TranslateAsync(text, sourceLanguage, targetLanguage).Result;
    }

    /// <summary>
    ///     Asynchronously translates the given text from source language to target
    ///     language using AWS Translate.
    /// </summary>
    /// <param name="text"></param>
    /// <param name="sourceLanguage"></param>
    /// <param name="targetLanguage"></param>
    /// <returns></returns>
    public async Task<string> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var fixedText = FixText(text);
        this.pluginLog.Debug($"AWS Translate input: {fixedText}");

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
            this.pluginLog.Debug($"AWS Translate result: {cleaned}");
            return cleaned;
        }
        catch (Exception ex)
        {
            this.pluginLog.Error($"AWS Translate error: {ex}");
            return string.Empty;
        }
    }
}
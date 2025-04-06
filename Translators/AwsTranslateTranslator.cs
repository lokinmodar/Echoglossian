using System;
using System.Threading.Tasks;

using Amazon;
using Amazon.Runtime;
using Amazon.Translate;
using Amazon.Translate.Model;
using Dalamud.Plugin.Services;

namespace Echoglossian.Translators
{
  public class AwsTranslateTranslator : ITranslator
  {
    private readonly IPluginLog pluginLog;
    private readonly Config config;
    private readonly AmazonTranslateClient translateClient;

    public AwsTranslateTranslator(IPluginLog pluginLog, Config config)
    {
      this.pluginLog = pluginLog;
      this.config = config;

      try
      {
        var region = RegionEndpoint.GetBySystemName(config.AwsRegion ?? "us-east-1");

        AWSCredentials credentials;

        if (!string.IsNullOrWhiteSpace(config.AwsAccessKey) && !string.IsNullOrWhiteSpace(config.AwsSecretKey))
        {
          credentials = new BasicAWSCredentials(config.AwsAccessKey, config.AwsSecretKey);
        }
        else
        {
          pluginLog.Warning("Using default AWS credentials provider chain.");
          credentials = FallbackCredentialsFactory.GetCredentials();
        }

        this.translateClient = new AmazonTranslateClient(credentials, new AmazonTranslateConfig
        {
          RegionEndpoint = region,
        });
      }
      catch (Exception ex)
      {
        this.pluginLog.Error($"Failed to initialize AWS Translate client: {ex}");
        throw;
      }
    }

    public string Translate(string text, string sourceLanguage, string targetLanguage)
    {
      this.pluginLog.Debug("AWS Translate sync translate requested.");
      return this.TranslateAsync(text, sourceLanguage, targetLanguage).Result;
    }

    public async Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
      if (string.IsNullOrWhiteSpace(text))
      {
        return string.Empty;
      }

      string fixedText = Echoglossian.FixText(text);
      this.pluginLog.Debug($"AWS Translate input: {fixedText}");

      try
      {
        var request = new TranslateTextRequest
        {
          Text = fixedText,
          SourceLanguageCode = sourceLanguage,
          TargetLanguageCode = targetLanguage,
        };

        var response = await this.translateClient.TranslateTextAsync(request);
        string cleaned = Echoglossian.FixText(response.TranslatedText);
        this.pluginLog.Debug($"AWS Translate result: {cleaned}");
        return cleaned;
      }
      catch (Exception ex)
      {
        this.pluginLog.Warning($"AWS Translate error: {ex}");
        return string.Empty;
      }
    }

  }
}

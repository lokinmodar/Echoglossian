using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Web;
using System.Diagnostics;

using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using Dalamud.Plugin.Services;

namespace Echoglossian.Translators
{
  public class YandexSeleniumTranslator : ITranslator, IDisposable
  {
    private readonly IPluginLog pluginLog;
    private readonly ChromeDriver driver;
    private bool isInitialized = false;
    private bool disposed;

    public YandexSeleniumTranslator(IPluginLog pluginLog)
    {
      this.pluginLog = pluginLog;

      var options = new ChromeOptions();
      options.AddArgument("--headless=new");
      options.AddArgument("--disable-gpu");
      options.AddArgument("--window-size=1,1");
      options.AddArgument("--no-sandbox");
      options.AddArgument("--disable-dev-shm-usage");
      options.AddUserProfilePreference("profile.managed_default_content_settings.images", 2);
      options.AddUserProfilePreference("profile.default_content_setting_values.notifications", 2);
      options.AddUserProfilePreference("profile.default_content_setting_values.stylesheets", 2);
      options.AddUserProfilePreference("profile.default_content_setting_values.plugins", 2);
      options.AddUserProfilePreference("profile.default_content_setting_values.popups", 2);
      options.AddUserProfilePreference("profile.default_content_setting_values.geolocation", 2);

      string basePath = Echoglossian.PluginInterface.AssemblyLocation.DirectoryName;
      string driverFileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
          ? "chromedriver.exe"
          : "chromedriver";
      string chromeDriverPath = Path.Combine(basePath, driverFileName);

      pluginLog.Debug($"ChromeDriver path: {chromeDriverPath}");

      if (!File.Exists(chromeDriverPath))
      {
        this.pluginLog.Warning("ChromeDriver not found, attempting download...");
        this.DownloadChromeDriverAsync(basePath, driverFileName).GetAwaiter().GetResult();

        if (!File.Exists(chromeDriverPath))
        {
          throw new FileNotFoundException($"ChromeDriver download failed or not found at: {chromeDriverPath}");
        }
      }

      this.driver = new ChromeDriver(basePath, options);
      this.pluginLog.Debug("YandexSeleniumTranslator initialized with headless Chrome (minimal visual content)");
    }

    private async Task DownloadChromeDriverAsync(string basePath, string driverFileName)
    {
      try
      {
        string chromeVersion = this.GetInstalledChromeVersion();
        string majorVersion = chromeVersion.Split('.')[0];

        string platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
          ? "win32"
          : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "mac-arm64" : "linux64";

        string url = $"https://storage.googleapis.com/chrome-for-testing-public/{chromeVersion}/{platform}/chromedriver-{platform}.zip";
        string zipPath = Path.Combine(basePath, "chromedriver.zip");

        using HttpClient client = new();
        var data = await client.GetByteArrayAsync(url);
        await File.WriteAllBytesAsync(zipPath, data);

        ZipFile.ExtractToDirectory(zipPath, basePath, true);
        File.Delete(zipPath);

        string extractedPath = this.FindDriverExecutable(basePath, driverFileName);
        string targetPath = Path.Combine(basePath, driverFileName);
        if (File.Exists(extractedPath))
        {
          File.Move(extractedPath, targetPath, true);
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
          Process.Start(new ProcessStartInfo
          {
            FileName = "chmod",
            Arguments = $"+x \"{targetPath}\"",
            UseShellExecute = false
          });
        }

        this.pluginLog.Debug($"Downloaded and extracted ChromeDriver from {url} to {targetPath}");
      }
      catch (Exception ex)
      {
        this.pluginLog.Warning($"Failed to download ChromeDriver: {ex.Message}");
      }
    }

    private string GetInstalledChromeVersion()
    {
      string? customPath = Environment.GetEnvironmentVariable("ECHOGLOSSIAN_CHROME_PATH");
      if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
      {
        this.pluginLog.Debug($"Using custom Chrome path from ECHOGLOSSIAN_CHROME_PATH: {customPath}");
        var versionInfo = FileVersionInfo.GetVersionInfo(customPath);
        return versionInfo.FileVersion ?? throw new Exception("Unable to determine Chrome version from custom path.");
      }
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      {
        string path = Environment.ExpandEnvironmentVariables("%ProgramFiles%\\Google\\Chrome\\Application\\chrome.exe");
        this.pluginLog.Debug($"Checking for Chrome at {path}...");
        if (!File.Exists(path))
        {
          path = Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%\\Google\\Chrome\\Application\\chrome.exe");
          this.pluginLog.Debug($"Checking for Chrome at {path}...");
        }
        if (!File.Exists(path))
        {
          this.pluginLog.Error($"Chrome is not installed at {path}");
          throw new FileNotFoundException($"Chrome is not installed at {path}.");
        }

        var versionInfo = FileVersionInfo.GetVersionInfo(path);
        return versionInfo.FileVersion ?? throw new Exception("Unable to determine Chrome version.");
      }
      else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      {
        var process = Process.Start(new ProcessStartInfo
        {
          FileName = "/usr/bin/defaults",
          Arguments = "read \"/Applications/Google Chrome.app/Contents/Info.plist\" CFBundleShortVersionString",
          RedirectStandardOutput = true,
          UseShellExecute = false,
        });
        process.WaitForExit();
        return process.StandardOutput.ReadToEnd().Trim();
      }
      else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
      {
        var process = Process.Start(new ProcessStartInfo
        {
          FileName = "google-chrome",
          Arguments = "--version",
          RedirectStandardOutput = true,
          UseShellExecute = false,
        });
        process.WaitForExit();
        var output = process.StandardOutput.ReadToEnd().Trim();
        return output.Replace("Google Chrome", string.Empty).Trim();
      }
      else
      {
        throw new PlatformNotSupportedException("Unsupported OS for Chrome version detection.");
      }
    }

    private string FindDriverExecutable(string directory, string expectedFileName)
    {
      foreach (string file in Directory.GetFiles(directory, expectedFileName, SearchOption.AllDirectories))
      {
        return file;
      }
      throw new FileNotFoundException($"Could not find extracted ChromeDriver executable: {expectedFileName}");
    }

    public string Translate(string text, string sourceLanguage, string targetLanguage)
    {
      return this.TranslateAsync(text, sourceLanguage, targetLanguage).Result;
    }

    public async Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
      return await Task.Run(() =>
      {
        try
        {
          string encodedText = HttpUtility.HtmlEncode(text);

          if (!this.isInitialized)
          {
            string initUrl = $"https://translate.yandex.com/?source_lang={sourceLanguage}&target_lang={targetLanguage}";
            this.driver.Navigate().GoToUrl(initUrl);
            this.pluginLog.Debug($"Navigated to: {initUrl}");
            this.isInitialized = true;
          }

          IJavaScriptExecutor js = this.driver;
          js.ExecuteScript(
            @"
            const input = document.querySelector('#fakeArea');
            if (input) {
              input.textContent = arguments[0];
              input.dispatchEvent(new Event('input', { bubbles: true }));
            }
          ",
            text);

          // this.driver.Navigate().GoToUrl(url); // no reload needed for future requests

          var wait = new WebDriverWait(this.driver, TimeSpan.FromSeconds(20));
          var resultElement = wait.Until(
              ExpectedConditions.ElementExists(By.CssSelector("[data-complaint-type='fullTextTranslation'] .translation-word, .measurer-text_main")));

          var translationChunks = resultElement.FindElements(By.CssSelector(".translation-word"));
          while (translationChunks.Count == 0)
          {
            Thread.Sleep(200);
            translationChunks = resultElement.FindElements(By.CssSelector(".translation-word"));
          }

          string result = string.Join(string.Empty, translationChunks
              .Select(e => e.Text)
              .Where(t => !string.IsNullOrWhiteSpace(t)));
          this.pluginLog.Debug($"Translation result: {result}");
          return result;
        }
        catch (Exception ex)
        {
          this.pluginLog.Warning($"YandexSeleniumTranslator failed: {ex.Message}");
          return string.Empty;
        }
      });
    }

    public void Dispose()
    {
      if (!this.disposed)
      {
        this.driver.Quit();
        this.driver.Dispose();
        this.pluginLog.Debug("YandexSeleniumTranslator disposed");
        this.disposed = true;
      }
    }
  }
}

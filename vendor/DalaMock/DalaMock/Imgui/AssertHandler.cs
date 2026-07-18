namespace DalaMock.Core.Imgui;

/// <summary>
/// Class responsible for registering and handling ImGui asserts.
/// </summary>
public class AssertHandler : IDisposable
{
    private readonly ILogger<AssertHandler> logger;
    private const int HideThreshold = 20;
    private const int HidePrintEvery = 500;

    private readonly HashSet<string> ignoredAsserts = [];
    private readonly Dictionary<string, uint> assertCounts = new();

    // Store callback to avoid it from being GC'd
    private readonly AssertCallbackDelegate callback;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssertHandler"/> class.
    /// </summary>
    public AssertHandler(ILogger<AssertHandler> logger)
    {
        this.logger = logger;
        this.callback = (expr, file, line) => this.OnImGuiAssert(expr, file, line);
    }

    private delegate void AssertCallbackDelegate(
        [MarshalAs(UnmanagedType.LPStr)] string expr,
        [MarshalAs(UnmanagedType.LPStr)] string file,
        int line);

    /// <summary>
    /// Gets or sets a value indicating whether ImGui asserts should be shown to the user.
    /// </summary>
    public bool ShowAsserts { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether we want to hide asserts that occur frequently (= every update)
    /// and whether we want to log callstacks.
    /// </summary>
    public bool EnableVerboseLogging { get; set; }

    /// <summary>
    /// Register the cimgui assert handler with the native library.
    /// </summary>
    public void Setup()
    {
        CustomNativeFunctions.igCustom_SetAssertCallback(this.callback);
    }

    /// <summary>
    /// Unregister the cimgui assert handler with the native library.
    /// </summary>
    public void Shutdown()
    {
        CustomNativeFunctions.igCustom_SetAssertCallback(null);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Shutdown();
    }

    private void OnImGuiAssert(string expr, string file, int line)
    {
        var key = $"{file}:{line}";
        if (this.ignoredAsserts.Contains(key))
        {
            return;
        }

        Lazy<string> stackTrace = new(() => new StackTrace(3).ToString());

        if (!this.EnableVerboseLogging)
        {
            if (this.assertCounts.TryGetValue(key, out var count))
            {
                this.assertCounts[key] = count + 1;

                if (count <= HideThreshold || count % HidePrintEvery == 0)
                {
                    this.logger.LogWarning(
                        "ImGui assertion failed: {Expr} at {File}:{Line} (repeated {Count} times)",
                        expr,
                        file,
                        line,
                        count);
                }
            }
            else
            {
                this.assertCounts[key] = 1;
            }
        }
        else
        {
            this.logger.LogWarning(
                "ImGui assertion failed: {Expr} at {File}:{Line}\n{StackTrace:l}",
                expr,
                file,
                line,
                stackTrace.Value);
        }
    }

    private static class CustomNativeFunctions
    {
        [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
#pragma warning disable SA1300
        public static extern void igCustom_SetAssertCallback(AssertCallbackDelegate? callback);
#pragma warning restore SA1300
    }
}

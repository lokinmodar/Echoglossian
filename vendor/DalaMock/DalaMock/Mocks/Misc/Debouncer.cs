namespace DalaMock.Core.Mocks.Misc;

/// <inheritdoc/>
internal class Debouncer : IDebouncer
{
    private readonly IFramework framework;
    private readonly TimeSpan delay;
    private readonly SysAction action;
    private readonly Lock debouncerLock = new();
    private CancellationTokenSource? cts;
    private DateTime targetTime = DateTime.MinValue;
    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="Debouncer"/> class.
    /// </summary>
    /// <param name="framework">Dalamuds <see cref="IFramework"/> service.</param>
    /// <param name="delay">The delay to wait after the last request before executing the action.</param>
    /// <param name="action">The delegate to execute when the debounce period elapses.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is <see langword="null"/>.</exception>
    public Debouncer(IFramework framework, TimeSpan delay, SysAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        this.framework = framework;
        this.delay = delay;
        this.action = action;
    }

    /// <inheritdoc/>
    public bool IsPending => this.cts != null;

    /// <inheritdoc/>
    public void Dispose()
    {
        using var scope = this.debouncerLock.EnterScope();

        if (this.isDisposed)
            return;

        this.Cancel();

        this.isDisposed = true;
    }

    /// <inheritdoc/>
    public void Debounce()
    {
        using var scope = this.debouncerLock.EnterScope();

        if (this.isDisposed)
            return;

        this.targetTime = DateTime.UtcNow + this.delay;

        if (this.IsPending)
            return;

        this.cts = new();
        this.framework.RunOnTick(this.OnTick, this.delay, cancellationToken: this.cts.Token);
    }

    /// <inheritdoc/>
    public void Cancel()
    {
        using var scope = this.debouncerLock.EnterScope();

        this.cts?.Cancel();
        this.cts?.Dispose();
        this.cts = null;
    }

    private void OnTick()
    {
        using (this.debouncerLock.EnterScope())
        {
            if (this.isDisposed)
                return;

            var now = DateTime.UtcNow;
            if (now < this.targetTime && this.cts != null)
            {
                this.framework.RunOnTick(this.OnTick, this.targetTime - now, cancellationToken: this.cts.Token);
                return;
            }

            this.cts?.Cancel();
            this.cts?.Dispose();
            this.cts = null;
        }

        this.action();
    }
}

// <copyright file="TranslationActivationCoordinator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

/// <summary>
/// Coordinates global translation activation through the Framework thread.
/// </summary>
internal sealed class TranslationActivationCoordinator
{
  private readonly Config configuration;
  private readonly Func<TranslationActivationGuard.BlockReason> getBlockReason;
  private readonly Action<Config> persistConfiguration;
  private readonly Func<Action, Task> runOnFrameworkThread;
  private readonly Action disableRuntime;
  private readonly Action enableRuntime;
  private readonly Action<Exception> observeFailure;
  private int transitionInProgress;
  private TranslationActivationState state;

  /// <summary>
  /// Initializes a new instance of the <see cref="TranslationActivationCoordinator" /> class.
  /// </summary>
  /// <param name="configuration">The persistent configuration source of truth.</param>
  /// <param name="getBlockReason">Resolves the current activation block reason.</param>
  /// <param name="persistConfiguration">Persists the updated configuration.</param>
  /// <param name="runOnFrameworkThread">Runs one complete transition on the Framework thread.</param>
  /// <param name="disableRuntime">Stops admissions and restores translated presentation.</param>
  /// <param name="enableRuntime">Reactivates and refreshes translated presentation.</param>
  /// <param name="observeFailure">Reports an unexpected transition failure.</param>
  public TranslationActivationCoordinator(
      Config configuration,
      Func<TranslationActivationGuard.BlockReason> getBlockReason,
      Action<Config> persistConfiguration,
      Func<Action, Task> runOnFrameworkThread,
      Action disableRuntime,
      Action enableRuntime,
      Action<Exception> observeFailure)
  {
    this.configuration = configuration;
    this.getBlockReason = getBlockReason;
    this.persistConfiguration = persistConfiguration;
    this.runOnFrameworkThread = runOnFrameworkThread;
    this.disableRuntime = disableRuntime;
    this.enableRuntime = enableRuntime;
    this.observeFailure = observeFailure;
    this.state = configuration.Translate
        ? TranslationActivationState.Enabled
        : TranslationActivationState.Disabled;
  }

  /// <summary>
  /// Gets the current global activation state.
  /// </summary>
  public TranslationActivationState State => this.state;

  /// <summary>
  /// Requests a global translation activation change.
  /// </summary>
  /// <param name="request">The requested state change.</param>
  /// <returns>The completed transition outcome.</returns>
  public async Task<TranslationActivationOutcome> RequestAsync(
      TranslationActivationRequest request)
  {
    if (Interlocked.CompareExchange(ref this.transitionInProgress, 1, 0) != 0)
    {
      return new TranslationActivationOutcome(
          TranslationActivationResult.TransitionInProgress,
          TranslationActivationGuard.BlockReason.None);
    }

    try
    {
      var targetEnabled = request switch
      {
        TranslationActivationRequest.Enable => true,
        TranslationActivationRequest.Disable => false,
        TranslationActivationRequest.Toggle => !this.configuration.Translate,
        _ => throw new ArgumentOutOfRangeException(nameof(request), request, null),
      };

      if (targetEnabled == this.configuration.Translate)
      {
        return new TranslationActivationOutcome(
            targetEnabled
                ? TranslationActivationResult.AlreadyEnabled
                : TranslationActivationResult.AlreadyDisabled,
            TranslationActivationGuard.BlockReason.None);
      }

      this.state = TranslationActivationState.Transitioning;
      var outcome = new TranslationActivationOutcome(
          TranslationActivationResult.Failed,
          TranslationActivationGuard.BlockReason.None);
      await this.runOnFrameworkThread(
          () => outcome = this.ApplyTransition(targetEnabled)).ConfigureAwait(false);
      return outcome;
    }
    catch (Exception exception)
    {
      this.observeFailure(exception);
      return new TranslationActivationOutcome(
          TranslationActivationResult.Failed,
          TranslationActivationGuard.BlockReason.None);
    }
    finally
    {
      this.state = this.configuration.Translate
          ? TranslationActivationState.Enabled
          : TranslationActivationState.Disabled;
      Interlocked.Exchange(ref this.transitionInProgress, 0);
    }
  }

  /// <summary>
  /// Applies one state change after dispatch to the Framework thread.
  /// </summary>
  /// <param name="targetEnabled">Whether translation should be enabled.</param>
  /// <returns>The completed transition outcome.</returns>
  private TranslationActivationOutcome ApplyTransition(bool targetEnabled)
  {
    if (targetEnabled)
    {
      var blockReason = this.getBlockReason();
      if (blockReason != TranslationActivationGuard.BlockReason.None)
      {
        return new TranslationActivationOutcome(
            TranslationActivationResult.Blocked,
            blockReason);
      }

      try
      {
        this.configuration.Translate = true;
        this.persistConfiguration(this.configuration);
        this.enableRuntime();
        return new TranslationActivationOutcome(
            TranslationActivationResult.Enabled,
            TranslationActivationGuard.BlockReason.None);
      }
      catch
      {
        this.configuration.Translate = false;
        try
        {
          this.persistConfiguration(this.configuration);
          this.disableRuntime();
        }
        catch (Exception)
        {
        }

        throw;
      }
    }

    this.configuration.Translate = false;
    this.persistConfiguration(this.configuration);
    this.disableRuntime();
    return new TranslationActivationOutcome(
        TranslationActivationResult.Disabled,
        TranslationActivationGuard.BlockReason.None);
  }
}

/// <summary>
/// Describes one requested global translation activation change.
/// </summary>
internal enum TranslationActivationRequest
{
  Enable,
  Disable,
  Toggle,
}

/// <summary>
/// Describes the currently visible translation activation state.
/// </summary>
internal enum TranslationActivationState
{
  Disabled,
  Enabled,
  Transitioning,
}

/// <summary>
/// Describes the result of one global translation activation request.
/// </summary>
internal enum TranslationActivationResult
{
  Enabled,
  Disabled,
  AlreadyEnabled,
  AlreadyDisabled,
  Blocked,
  TransitionInProgress,
  Failed,
}

/// <summary>
/// Carries the result and optional activation block reason for one request.
/// </summary>
/// <param name="Status">The completed request status.</param>
/// <param name="BlockReason">The activation block reason, when applicable.</param>
internal sealed record TranslationActivationOutcome(
    TranslationActivationResult Status,
    TranslationActivationGuard.BlockReason BlockReason);

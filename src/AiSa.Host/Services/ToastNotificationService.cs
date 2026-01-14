using Microsoft.FluentUI.AspNetCore.Components;

namespace AiSa.Host.Services;

/// <summary>
/// Implementation of IToastNotificationService that wraps Fluent UI IToastService.
/// Provides convenience methods and consistent timeout defaults.
/// </summary>
public class ToastNotificationService : IToastNotificationService
{
    private readonly IToastService _toastService;

    // Default timeouts based on intent severity
    private const int DefaultSuccessTimeout = 5000; // 5 seconds
    private const int DefaultInfoTimeout = 5000;    // 5 seconds
    private const int DefaultWarningTimeout = 7000; // 7 seconds
    private const int DefaultErrorTimeout = 10000;  // 10 seconds

    public ToastNotificationService(IToastService toastService)
    {
        _toastService = toastService ?? throw new ArgumentNullException(nameof(toastService));
    }

    /// <inheritdoc/>
    public void ShowSuccess(string message, string? title = null, int? timeout = null)
    {
        _toastService.ShowToast(
            ToastIntent.Success,
            message,
            timeout ?? DefaultSuccessTimeout,
            title);
    }

    /// <inheritdoc/>
    public void ShowInfo(string message, string? title = null, int? timeout = null)
    {
        _toastService.ShowToast(
            ToastIntent.Info,
            message,
            timeout ?? DefaultInfoTimeout,
            title);
    }

    /// <inheritdoc/>
    public void ShowWarning(string message, string? title = null, int? timeout = null)
    {
        _toastService.ShowToast(
            ToastIntent.Warning,
            message,
            timeout ?? DefaultWarningTimeout,
            title);
    }

    /// <inheritdoc/>
    public void ShowError(string message, string? title = null, int? timeout = null)
    {
        _toastService.ShowToast(
            ToastIntent.Error,
            message,
            timeout ?? DefaultErrorTimeout,
            title);
    }

    /// <inheritdoc/>
    public void ShowToast(ToastIntent intent, string message, string? title = null, int? timeout = null)
    {
        var defaultTimeout = intent switch
        {
            ToastIntent.Success => DefaultSuccessTimeout,
            ToastIntent.Info => DefaultInfoTimeout,
            ToastIntent.Warning => DefaultWarningTimeout,
            ToastIntent.Error => DefaultErrorTimeout,
            _ => DefaultInfoTimeout
        };

        _toastService.ShowToast(
            intent,
            message,
            timeout ?? defaultTimeout,
            title);
    }
}


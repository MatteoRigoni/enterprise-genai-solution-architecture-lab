using Microsoft.FluentUI.AspNetCore.Components;

namespace AiSa.Host.Services;

/// <summary>
/// Service for centralized toast notification management.
/// Wraps Fluent UI IToastService with convenience methods and consistent patterns.
/// </summary>
public interface IToastNotificationService
{
    /// <summary>
    /// Shows a success toast notification.
    /// </summary>
    /// <param name="message">The main message to display.</param>
    /// <param name="title">Optional title for the toast.</param>
    /// <param name="timeout">Timeout in milliseconds. Default is 5000ms.</param>
    void ShowSuccess(string message, string? title = null, int? timeout = null);

    /// <summary>
    /// Shows an information toast notification.
    /// </summary>
    /// <param name="message">The main message to display.</param>
    /// <param name="title">Optional title for the toast.</param>
    /// <param name="timeout">Timeout in milliseconds. Default is 5000ms.</param>
    void ShowInfo(string message, string? title = null, int? timeout = null);

    /// <summary>
    /// Shows a warning toast notification.
    /// </summary>
    /// <param name="message">The main message to display.</param>
    /// <param name="title">Optional title for the toast.</param>
    /// <param name="timeout">Timeout in milliseconds. Default is 7000ms.</param>
    void ShowWarning(string message, string? title = null, int? timeout = null);

    /// <summary>
    /// Shows an error toast notification.
    /// </summary>
    /// <param name="message">The main message to display.</param>
    /// <param name="title">Optional title for the toast.</param>
    /// <param name="timeout">Timeout in milliseconds. Default is 10000ms.</param>
    void ShowError(string message, string? title = null, int? timeout = null);

    /// <summary>
    /// Shows a toast notification with custom intent and options.
    /// </summary>
    /// <param name="intent">The intent/severity of the toast.</param>
    /// <param name="message">The main message to display.</param>
    /// <param name="title">Optional title for the toast.</param>
    /// <param name="timeout">Optional timeout in milliseconds.</param>
    void ShowToast(ToastIntent intent, string message, string? title = null, int? timeout = null);
}


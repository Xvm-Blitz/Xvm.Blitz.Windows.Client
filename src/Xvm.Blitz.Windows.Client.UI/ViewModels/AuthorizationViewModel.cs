using System.Net;
using System.Windows.Input;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Xvm.Blitz.Windows.Client.Core.Models;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions.Authorization;

namespace Xvm.Blitz.Windows.Client.UI.ViewModels;

public class AuthorizationViewModel : ReactiveObject, IDisposable
{
    private readonly IAuthorizationService _authorizationService;

    private readonly IPresenceRuntimeService _presenceRuntimeService;

    private readonly ILogger<AuthorizationViewModel> _logger;

    private readonly Timer _refreshTimer;

    private readonly IUsageService _usageService;

    private bool _isAuthenticated;

    private bool _isConfirmationVisible;

    private bool _isErrorPopupVisible;

    private bool _isLoading;

    private bool _isQuotaLoading;

    private bool _isQuotaAvailable;

    private GetUsageResponseDto? _quotaInfo;

    private string? _statusMessage;

    public ICommand LoginWithOpenIdCommand { get; }

    public ICommand LogoutCommand { get; }

    public ICommand ConfirmLogoutCommand { get; }

    public ICommand CancelLogoutCommand { get; }

    public ICommand DismissErrorCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            this.RaisePropertyChanged();
        }
    }

    public bool IsQuotaLoading
    {
        get => _isQuotaLoading;
        set
        {
            _isQuotaLoading = value;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(IsQuotaAvailableAndNotLoading));
            this.RaisePropertyChanged(nameof(IsQuotaNotAvailableAndNotLoading));
        }
    }

    public bool IsQuotaAvailableAndNotLoading => IsQuotaAvailable && !IsQuotaLoading;

    public bool IsQuotaNotAvailableAndNotLoading => !IsQuotaAvailable && !IsQuotaLoading;

    public string? StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            this.RaisePropertyChanged();
        }
    }

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        set
        {
            _isAuthenticated = value;
            this.RaisePropertyChanged();
        }
    }

    public GetUsageResponseDto? QuotaInfo
    {
        get => _quotaInfo;
        set
        {
            _quotaInfo = value;
            if (this.RaiseAndSetIfChanged(ref _quotaInfo, value) != value)
                return;

            this.RaisePropertyChanged(nameof(QuotaStatusText));
            this.RaisePropertyChanged(nameof(IsQuotaAvailable));
            this.RaisePropertyChanged(nameof(IsQuotaLow));
            this.RaisePropertyChanged(nameof(IsQuotaCritical));
            this.RaisePropertyChanged(nameof(FormattedPeriod));
            this.RaisePropertyChanged(nameof(RemainingTimeText));
            this.RaisePropertyChanged(nameof(RemainingRequests));
            this.RaisePropertyChanged(nameof(MonthlyLimit));
            this.RaisePropertyChanged(nameof(UsedRequests));
            this.RaisePropertyChanged(nameof(UsagePercentage));
            this.RaisePropertyChanged(nameof(LastUpdatedQuotaDateTime));
        }
    }

    public bool IsQuotaAvailable
    {
        get => _isQuotaAvailable;
        set
        {
            _isQuotaAvailable = value;
            this.RaiseAndSetIfChanged(ref _isQuotaAvailable, value);
            this.RaisePropertyChanged(nameof(IsQuotaAvailableAndNotLoading));
            this.RaisePropertyChanged(nameof(IsQuotaNotAvailableAndNotLoading));
        }
    }

    public bool IsConfirmationVisible
    {
        get => _isConfirmationVisible;
        set
        {
            _isConfirmationVisible = value;
            this.RaisePropertyChanged();
        }
    }

    public bool IsErrorPopupVisible
    {
        get => _isErrorPopupVisible;
        set => this.RaiseAndSetIfChanged(ref _isErrorPopupVisible, value);
    }

    public string QuotaStatusText =>
        QuotaInfo is null ? "Информация об использовании отсутствует" : $"Использовано: {UsedRequests} из {MonthlyLimit} ({UsagePercentage:F1}%)";

    public bool IsQuotaLow => UsagePercentage is >= 80 and < 95;

    public bool IsQuotaCritical => UsagePercentage >= 95;

    public string FormattedPeriod => QuotaInfo is null ? string.Empty : $"{QuotaInfo.PeriodStart:dd.MM.yyyy} - {QuotaInfo.PeriodEnd:dd.MM.yyyy}";

    public string? LastUpdatedQuotaDateTime { get; set; }

    public string? RemainingTimeText
    {
        get
        {
            if (QuotaInfo is null)
                return null;

            var remaining = QuotaInfo.PeriodEnd - DateTimeOffset.UtcNow;
            if (remaining.TotalDays >= 1)
                return $"Осталось дней: {remaining.Days}";

            return remaining.TotalHours >= 1
                ? $"Осталось часов: {remaining.Hours}"
                : $"Осталось минут: {remaining.Minutes}";
        }
    }

    public int RemainingRequests => (QuotaInfo?.TotalLimit ?? 0) - (QuotaInfo?.CurrentUsage ?? 0);

    public int MonthlyLimit => QuotaInfo?.TotalLimit ?? 0;

    public int UsedRequests => QuotaInfo?.CurrentUsage ?? 0;

    public double UsagePercentage => MonthlyLimit > 0 ? (double)UsedRequests / MonthlyLimit * 100 : 0;

    public AuthorizationViewModel(
        IAuthorizationService authorizationService,
        IPresenceRuntimeService presenceRuntimeService,
        IUsageService usageService,
        ILogger<AuthorizationViewModel> logger)
    {
        _authorizationService = authorizationService;
        _presenceRuntimeService = presenceRuntimeService;
        _usageService = usageService;
        _logger = logger;

        LoginWithOpenIdCommand = ReactiveCommand.CreateFromTask(LoginWithOpenIdAsync);
        LogoutCommand = ReactiveCommand.Create(ShowLogoutConfirmation);
        ConfirmLogoutCommand = ReactiveCommand.CreateFromTask(ConfirmLogoutAsync);
        CancelLogoutCommand = ReactiveCommand.Create(CancelLogout);
        DismissErrorCommand = ReactiveCommand.Create(DismissError);

        _refreshTimer = new Timer(
            _ => Dispatcher.UIThread.InvokeAsync(RefreshQuotaAsync),
            null,
            Timeout.Infinite,
            Timeout.Infinite);

        _ = InitializeAsync();
    }

    public void Dispose()
    {
        _refreshTimer.Dispose();
    }

    private async Task InitializeAsync()
    {
        try
        {
            IsAuthenticated = _authorizationService.IsAuthenticated;

            if (IsAuthenticated)
            {
                StartQuotaRefresh();
                await RefreshQuotaAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing AuthorizationViewModel");
        }
    }

    private async Task LoginWithOpenIdAsync()
    {
        if (IsLoading)
            return;

        try
        {
            IsLoading = true;
            StatusMessage = "Откройте браузер и войдите через Lesta...";

            var success = await _authorizationService.LoginWithOpenIdAsync();
            if (success)
            {
                IsAuthenticated = true;
                await _presenceRuntimeService.StartAsync();
                StartQuotaRefresh();
                await RefreshQuotaAsync();
                StatusMessage = "Вход через Lesta выполнен.";
            }
            else
            {
                StatusMessage = "Не удалось войти через Lesta OpenID. Если ошибка повторяется — закройте другие экземпляры клиента и попробуйте снова.";
            }
        }
        catch (Exception exception)
        {
            StatusMessage = "Произошла ошибка при авторизации. Повторите попытку позже.";
            _logger.LogError(exception, "Error authorizing with OpenID");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LogoutAsync()
    {
        try
        {
            await _presenceRuntimeService.StopAsync();
            await _authorizationService.Logout();
            StatusMessage = "Выход выполнен успешно.";
            IsAuthenticated = false;
            IsQuotaAvailable = false;
            QuotaInfo = null;
            IsConfirmationVisible = false;
            LastUpdatedQuotaDateTime = null;
            StopQuotaRefresh();
        }
        catch (Exception exception)
        {
            StatusMessage = "Ошибка при выходе из системы. Повторите попытку позже";
            _logger.LogError(exception, "Error signing out");
        }
    }

    private void ShowLogoutConfirmation()
    {
        IsConfirmationVisible = true;
    }

    private async Task ConfirmLogoutAsync()
    {
        await LogoutAsync();
    }

    private void CancelLogout()
    {
        IsConfirmationVisible = false;
    }

    private void DismissError()
    {
        IsErrorPopupVisible = false;
    }

    private void ShowError(string message)
    {
        StatusMessage = message;
        IsErrorPopupVisible = true;
    }

    private void StartQuotaRefresh()
    {
        _refreshTimer.Change(TimeSpan.Zero, TimeSpan.FromMinutes(5));
    }

    private void StopQuotaRefresh()
    {
        _refreshTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private async Task RefreshQuotaAsync()
    {
        if (!IsAuthenticated || LastUpdatedQuotaDateTime is not null)
            return;

        try
        {
            IsQuotaLoading = true;
            LastUpdatedQuotaDateTime = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
            var quotaInfo = await _usageService.Get();
            IsQuotaAvailable = quotaInfo is not null;

            if (IsQuotaAvailable)
            {
                QuotaInfo = quotaInfo;
                StatusMessage = $"Обновлено: {LastUpdatedQuotaDateTime}";
            }
            else
            {
                ShowError("Не удалось получить информацию об использовании");
                _logger.LogWarning("Failed to get usage information");
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized)
        {
            ShowError("Сессия недействительна. Войдите снова через Lesta OpenID.");
        }
        catch (HttpRequestException ex)
        {
            ShowError(string.IsNullOrWhiteSpace(ex.Message)
                ? "Сервер статистики недоступен, повторите попытку позже"
                : ex.Message);
        }
        catch (Exception)
        {
            ShowError("Произошла ошибка при обновлении информации об использовании.");
        }
        finally
        {
            IsQuotaLoading = false;
        }
    }
}

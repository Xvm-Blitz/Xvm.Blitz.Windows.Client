using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using System.Windows.Input;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Xvm.Blitz.Windows.Client.Core.Models;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions.Authorization;
using Xvm.Blitz.Windows.Client.UI.Windows;

namespace Xvm.Blitz.Windows.Client.UI.ViewModels;

public class AuthorizationViewModel : ReactiveObject, IDisposable
{
    private readonly IAuthorizationService _authorizationService;

    private readonly IPresenceRuntimeService _presenceRuntimeService;

    private readonly ILogger<AuthorizationViewModel> _logger;

    private readonly Timer _refreshTimer;

    private readonly IUsageService _usageService;

    private readonly ISubscriptionService _subscriptionService;

    private CancellationTokenSource? _paymentPollingCts;

    private CancellationTokenSource? _paymentStatusMessageClearCts;

    private bool _isAuthenticated;

    private bool _isConfirmationVisible;

    private bool _isErrorPopupVisible;

    private bool _isLoading;

    private bool _isQuotaLoading;

    private bool _isQuotaAvailable;

    private bool _isPaymentCreating;

    private bool _isPaymentPending;

    private GetUsageResponseDto? _quotaInfo;

    private GetSubscriptionUserPricingResponseDto? _subscriptionPricing;

    private GetSubscriptionPublicPricingResponseDto? _publicPricing;

    private string? _statusMessage;

    private string? _paymentStatusMessage;

    private string _receiptEmail = string.Empty;

    public ICommand LoginWithOpenIdCommand { get; }

    public ICommand LogoutCommand { get; }

    public ICommand ConfirmLogoutCommand { get; }

    public ICommand CancelLogoutCommand { get; }

    public ICommand DismissErrorCommand { get; }

    public ICommand CreatePaymentCommand { get; }

    public ICommand RefreshAccountCommand { get; }

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

    public bool IsPaymentCreating
    {
        get => _isPaymentCreating;
        set
        {
            _isPaymentCreating = value;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(CanCreatePayment));
        }
    }

    public bool IsPaymentPending
    {
        get => _isPaymentPending;
        set
        {
            _isPaymentPending = value;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(CanCreatePayment));
        }
    }

    public bool CanCreatePayment => !IsPaymentCreating && !IsPaymentPending;

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

    public string? PaymentStatusMessage
    {
        get => _paymentStatusMessage;
        set
        {
            _paymentStatusMessage = value;
            this.RaisePropertyChanged();
        }
    }

    public string ReceiptEmail
    {
        get => _receiptEmail;
        set
        {
            _receiptEmail = value;
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
            if (EqualityComparer<GetUsageResponseDto?>.Default.Equals(_quotaInfo, value))
                return;

            this.RaiseAndSetIfChanged(ref _quotaInfo, value);

            this.RaisePropertyChanged(nameof(QuotaStatusText));
            this.RaisePropertyChanged(nameof(IsQuotaLow));
            this.RaisePropertyChanged(nameof(IsQuotaCritical));
            this.RaisePropertyChanged(nameof(FormattedPeriod));
            this.RaisePropertyChanged(nameof(RemainingTimeText));
            this.RaisePropertyChanged(nameof(RemainingRequests));
            this.RaisePropertyChanged(nameof(MonthlyLimit));
            this.RaisePropertyChanged(nameof(UsedRequests));
            this.RaisePropertyChanged(nameof(UsagePercentage));
            this.RaisePropertyChanged(nameof(FormattedAccountType));
            this.RaisePropertyChanged(nameof(FormattedTariffPrice));
            this.RaisePropertyChanged(nameof(IsPremiumActive));
            this.RaisePropertyChanged(nameof(IsFreeOrTrialTariff));
            this.RaisePropertyChanged(nameof(FormattedPremiumUntil));
            this.RaisePropertyChanged(nameof(FormattedNextPaymentPeriod));
            this.RaisePropertyChanged(nameof(IsTariffBlockVisible));
            this.RaisePropertyChanged(nameof(IsPaymentBlockVisible));
        }
    }

    public GetSubscriptionUserPricingResponseDto? SubscriptionPricing
    {
        get => _subscriptionPricing;
        set
        {
            if (EqualityComparer<GetSubscriptionUserPricingResponseDto?>.Default.Equals(_subscriptionPricing, value))
                return;

            this.RaiseAndSetIfChanged(ref _subscriptionPricing, value);

            this.RaisePropertyChanged(nameof(FormattedPaymentPrice));
            this.RaisePropertyChanged(nameof(FormattedTariffPrice));
            this.RaisePropertyChanged(nameof(FormattedPremiumUntil));
            this.RaisePropertyChanged(nameof(FormattedNextPaymentPeriod));
            this.RaisePropertyChanged(nameof(GrandfatheredPriceHint));
            this.RaisePropertyChanged(nameof(IsGrandfatheredPriceVisible));
            this.RaisePropertyChanged(nameof(IsPremiumActive));
            this.RaisePropertyChanged(nameof(IsFreeOrTrialTariff));
            this.RaisePropertyChanged(nameof(IsPaymentBlockVisible));
        }
    }

    public GetSubscriptionPublicPricingResponseDto? PublicPricing
    {
        get => _publicPricing;
        set
        {
            if (EqualityComparer<GetSubscriptionPublicPricingResponseDto?>.Default.Equals(_publicPricing, value))
                return;

            this.RaiseAndSetIfChanged(ref _publicPricing, value);

            this.RaisePropertyChanged(nameof(GrandfatheredPriceHint));
            this.RaisePropertyChanged(nameof(IsGrandfatheredPriceVisible));
            this.RaisePropertyChanged(nameof(FormattedPaymentPrice));
            this.RaisePropertyChanged(nameof(FormattedTariffPrice));
            this.RaisePropertyChanged(nameof(IsPaymentBlockVisible));
        }
    }

    public bool IsQuotaAvailable
    {
        get => _isQuotaAvailable;
        set
        {
            if (_isQuotaAvailable == value)
                return;

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

    public string FormattedAccountType =>
        QuotaInfo?.Type switch
        {
            AccessType.Free or AccessType.Trial => "Тариф: Бесплатный",
            AccessType.FullAccess => "Тариф: Премиум",
            _ => "Тариф: -",
        };

    public bool IsFreeOrTrialTariff =>
        QuotaInfo?.Type is AccessType.Free or AccessType.Trial;

    public bool IsPremiumActive =>
        QuotaInfo?.Type is AccessType.FullAccess &&
        (SubscriptionPricing?.PremiumUntil is null || SubscriptionPricing.PremiumUntil > DateTimeOffset.UtcNow);

    public bool IsTariffBlockVisible => QuotaInfo is not null;

    public bool IsPaymentBlockVisible =>
        QuotaInfo is not null && (SubscriptionPricing is not null || PublicPricing is not null);

    public string FormattedTariffPrice =>
        IsFreeOrTrialTariff
            ? $"0 {FormatCurrency(SubscriptionPricing?.Currency ?? PublicPricing?.Currency ?? "RUB")} / мес"
            : SubscriptionPricing is null
                ? string.Empty
                : $"{SubscriptionPricing.Amount:0} {FormatCurrency(SubscriptionPricing.Currency)} / мес";

    public string FormattedPaymentPrice =>
        SubscriptionPricing is not null
            ? $"{SubscriptionPricing.Amount:0} {FormatCurrency(SubscriptionPricing.Currency)} / мес"
            : PublicPricing is not null
                ? $"{PublicPricing.Amount:0} {FormatCurrency(PublicPricing.Currency)} / мес"
                : string.Empty;

    public string FormattedPremiumUntil
    {
        get
        {
            if (IsPremiumActive)
            {
                return SubscriptionPricing?.PremiumUntil is null
                    ? "Не оплачена"
                    : $"Оплачена до: {SubscriptionPricing.PremiumUntil.Value.ToLocalTime():dd.MM.yyyy}";
            }

            if (IsFreeOrTrialTariff && QuotaInfo is not null)
                return $"Квота будет обновлена после {QuotaInfo.PeriodEnd.ToLocalTime():dd.MM.yyyy}";

            return SubscriptionPricing?.PremiumUntil is null
                ? "Не оплачена"
                : $"Оплачена до: {SubscriptionPricing.PremiumUntil.Value.ToLocalTime():dd.MM.yyyy}";
        }
    }

    public string FormattedNextPaymentPeriod
    {
        get
        {
            if (IsFreeOrTrialTariff)
            {
                var periodStart = DateTimeOffset.Now.Date;
                var periodEnd = periodStart.AddMonths(1);

                return $"Оплачиваемый период: {FormatPeriodRange(periodStart, periodEnd)}";
            }

            if (SubscriptionPricing is null)
                return string.Empty;

            return $"Следующий оплачиваемый период: {FormatPeriodRange(SubscriptionPricing.NextPaymentPeriod.Start, SubscriptionPricing.NextPaymentPeriod.End)}";
        }
    }

    public bool IsGrandfatheredPriceVisible =>
        SubscriptionPricing?.IsGrandfathered == true &&
        SubscriptionPricing.LegacyPriceUntil is not null &&
        PublicPricing is not null &&
        PublicPricing.Amount != SubscriptionPricing.Amount;

    public string GrandfatheredPriceHint =>
        !IsGrandfatheredPriceVisible || SubscriptionPricing?.LegacyPriceUntil is null || PublicPricing is null
            ? string.Empty
            : $"Льготная цена действует до {SubscriptionPricing.LegacyPriceUntil.Value.ToLocalTime():dd.MM.yyyy}. Затем - {PublicPricing.Amount:0} {FormatCurrency(PublicPricing.Currency)} / мес";

    public string QuotaStatusText =>
        QuotaInfo is null ? "Информация об использовании отсутствует" : $"Использовано: {UsedRequests} из {MonthlyLimit} ({UsagePercentage:F1}%)";

    public bool IsQuotaLow => UsagePercentage is >= 80 and < 95;

    public bool IsQuotaCritical => UsagePercentage >= 95;

    public string FormattedPeriod => QuotaInfo is null ? string.Empty : FormatPeriodRange(QuotaInfo.PeriodStart, QuotaInfo.PeriodEnd);

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
        ISubscriptionService subscriptionService,
        ILogger<AuthorizationViewModel> logger)
    {
        _authorizationService = authorizationService;
        _presenceRuntimeService = presenceRuntimeService;
        _usageService = usageService;
        _subscriptionService = subscriptionService;
        _logger = logger;

        LoginWithOpenIdCommand = ReactiveCommand.CreateFromTask(LoginWithOpenIdAsync);
        LogoutCommand = ReactiveCommand.Create(ShowLogoutConfirmation);
        ConfirmLogoutCommand = ReactiveCommand.CreateFromTask(ConfirmLogoutAsync);
        CancelLogoutCommand = ReactiveCommand.Create(CancelLogout);
        DismissErrorCommand = ReactiveCommand.Create(DismissError);
        CreatePaymentCommand = ReactiveCommand.CreateFromTask(CreatePaymentAsync);
        RefreshAccountCommand = ReactiveCommand.CreateFromTask(RefreshAccountAsync);

        _refreshTimer = new Timer(
            _ => Dispatcher.UIThread.InvokeAsync(RefreshAccountAsync),
            null,
            Timeout.Infinite,
            Timeout.Infinite);

        _ = InitializeAsync();
    }

    public void Dispose()
    {
        StopPaymentPolling();
        _refreshTimer.Dispose();
    }

    private async Task InitializeAsync()
    {
        try
        {
            IsAuthenticated = _authorizationService.IsAuthenticated;

            if (IsAuthenticated)
            {
                StartAccountRefresh();
                await RefreshAccountAsync();
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
                StartAccountRefresh();
                await RefreshAccountAsync();
                StatusMessage = "Вход через Lesta выполнен.";
            }
            else
            {
                StatusMessage = "Не удалось войти через Lesta OpenID. Если ошибка повторяется - закройте другие экземпляры клиента и попробуйте снова.";
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
            StopPaymentPolling();
            CancelPaymentStatusMessageClear();
            await _presenceRuntimeService.StopAsync();
            await _authorizationService.Logout();
            App.ServiceProvider.GetRequiredService<IVoiceRuntimeService>().SetPremium(false);
            StatusMessage = "Выход выполнен успешно.";
            IsAuthenticated = false;
            IsQuotaAvailable = false;
            QuotaInfo = null;
            SubscriptionPricing = null;
            PublicPricing = null;
            PaymentStatusMessage = null;
            IsConfirmationVisible = false;
            LastUpdatedQuotaDateTime = null;
            StopAccountRefresh();
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

    private void StartAccountRefresh()
    {
        _refreshTimer.Change(TimeSpan.Zero, TimeSpan.FromMinutes(5));
    }

    private void StopAccountRefresh()
    {
        _refreshTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private async Task RefreshAccountAsync()
    {
        if (!IsAuthenticated)
            return;

        try
        {
            IsQuotaLoading = true;

            GetUsageResponseDto? quotaInfo = null;
            GetSubscriptionUserPricingResponseDto? subscriptionPricing = null;
            GetSubscriptionPublicPricingResponseDto? publicPricing = null;

            try
            {
                quotaInfo = await _usageService.Get();
            }
            catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized)
            {
                ShowError("Сессия недействительна. Войдите снова через Lesta OpenID.");
                return;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Failed to get usage information");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get usage information");
            }

            try
            {
                subscriptionPricing = await _subscriptionService.GetUserPricingAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get subscription pricing");
            }

            try
            {
                publicPricing = await _subscriptionService.GetPublicPricingAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get public subscription pricing");
            }

            if (quotaInfo is not null)
            {
                IsQuotaAvailable = true;
                QuotaInfo = quotaInfo;
                App.ServiceProvider.GetRequiredService<IVoiceRuntimeService>()
                    .SetPremium(quotaInfo.Type is AccessType.FullAccess);
            }
            else if (QuotaInfo is null)
            {
                IsQuotaAvailable = false;
                ShowError("Не удалось получить информацию об использовании");
                _logger.LogWarning("Failed to get usage information");
            }

            SubscriptionPricing = subscriptionPricing;
            PublicPricing = publicPricing;
            LastUpdatedQuotaDateTime = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
            StatusMessage = $"Обновлено: {LastUpdatedQuotaDateTime}";
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

    private async Task CreatePaymentAsync()
    {
        if (!CanCreatePayment)
            return;

        var normalizedReceiptEmail = ReceiptEmail.Trim();
        if (!IsValidReceiptEmail(normalizedReceiptEmail))
        {
            PaymentStatusMessage = "Укажите корректный email для чека";
            return;
        }

        try
        {
            IsPaymentCreating = true;
            CancelPaymentStatusMessageClear();
            PaymentStatusMessage = "Создание платежа...";

            var payment = await _subscriptionService.CreatePaymentAsync(normalizedReceiptEmail);
            if (payment is null)
            {
                PaymentStatusMessage = "Не удалось создать платёж";
                return;
            }

            Process.Start(new ProcessStartInfo(payment.ConfirmationUrl) { UseShellExecute = true });
            PaymentStatusMessage = $"Откройте браузер для оплаты {payment.Amount:0} {FormatCurrency(payment.Currency)} за период до {payment.PeriodEnd.ToLocalTime():dd.MM.yyyy}";
            StartPaymentPolling(payment.PaymentId);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Conflict)
        {
            PaymentStatusMessage = string.IsNullOrWhiteSpace(ex.Message)
                ? "Уже есть незавершённый платёж. Завершите или отмените его."
                : ex.Message;
        }
        catch (HttpRequestException ex)
        {
            PaymentStatusMessage = string.IsNullOrWhiteSpace(ex.Message)
                ? "Не удалось создать платёж"
                : ex.Message;
        }
        catch (Exception exception)
        {
            PaymentStatusMessage = "Произошла ошибка при создании платежа";
            _logger.LogError(exception, "Error creating subscription payment");
        }
        finally
        {
            IsPaymentCreating = false;
        }
    }

    private void StartPaymentPolling(Guid paymentId)
    {
        StopPaymentPolling();
        _paymentPollingCts = new CancellationTokenSource();
        var token = _paymentPollingCts.Token;
        IsPaymentPending = true;

        _ = Task.Run(async () =>
        {
            try
            {
                for (var attempt = 0; attempt < 100 && !token.IsCancellationRequested; attempt++)
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), token);

                    var status = await _subscriptionService.GetPaymentAsync(paymentId, token);
                    if (status is null)
                        continue;

                    if (status.Status is SubscriptionPaymentStatus.Succeeded)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            PaymentStatusMessage = "Оплата прошла успешно";
                            SchedulePaymentSuccessMessageClear();
                        });
                        await Dispatcher.UIThread.InvokeAsync(RefreshAccountAsync);
                        break;
                    }

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        PaymentStatusMessage = status.Status switch
                        {
                            SubscriptionPaymentStatus.Pending => "Ожидание оплаты...",
                            SubscriptionPaymentStatus.Canceled => "Платёж отменён",
                            SubscriptionPaymentStatus.PaymentMismatch => "Ошибка сверки платежа. Обратитесь в поддержку.",
                            _ => PaymentStatusMessage,
                        };
                    });

                    if (status.Status is SubscriptionPaymentStatus.Canceled or SubscriptionPaymentStatus.PaymentMismatch)
                        break;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error polling subscription payment status");
            }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(() => IsPaymentPending = false);
            }
        }, token);
    }

    private void StopPaymentPolling()
    {
        _paymentPollingCts?.Cancel();
        _paymentPollingCts?.Dispose();
        _paymentPollingCts = null;
        IsPaymentPending = false;
    }

    private void SchedulePaymentSuccessMessageClear()
    {
        CancelPaymentStatusMessageClear();
        _paymentStatusMessageClearCts = new CancellationTokenSource();
        var token = _paymentStatusMessageClearCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), token);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (PaymentStatusMessage == "Оплата прошла успешно")
                        PaymentStatusMessage = null;
                });
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private void CancelPaymentStatusMessageClear()
    {
        _paymentStatusMessageClearCts?.Cancel();
        _paymentStatusMessageClearCts?.Dispose();
        _paymentStatusMessageClearCts = null;
    }

    private static string FormatPeriodRange(DateTimeOffset start, DateTimeOffset end) =>
        $"{start.ToLocalTime():dd.MM.yyyy}\u00A0-\u00A0{end.ToLocalTime():dd.MM.yyyy}";

    private static string FormatPeriodRange(DateTime start, DateTime end) =>
        $"{start:dd.MM.yyyy}\u00A0-\u00A0{end:dd.MM.yyyy}";

    private static string FormatCurrency(string currency) =>
        currency.Equals("RUB", StringComparison.OrdinalIgnoreCase) ? "₽" : currency;

    private static bool IsValidReceiptEmail(string email) =>
        !string.IsNullOrWhiteSpace(email) && MailAddress.TryCreate(email, out _);
}

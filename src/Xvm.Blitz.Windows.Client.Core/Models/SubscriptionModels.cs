using System.Text.Json.Serialization;

namespace Xvm.Blitz.Windows.Client.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SubscriptionPaymentStatus
{
    Pending = 1,

    Succeeded = 2,

    Canceled = 3,

    PaymentMismatch = 4,
}

public sealed record GetSubscriptionPublicPricingResponseDto(
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("billing_period")] string BillingPeriod);

public sealed record GetSubscriptionUserPricingResponseDto(
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("billing_period")] string BillingPeriod,
    [property: JsonPropertyName("is_grandfathered")] bool IsGrandfathered,
    [property: JsonPropertyName("premium_until")] DateTimeOffset? PremiumUntil,
    [property: JsonPropertyName("legacy_price_until")] DateTimeOffset? LegacyPriceUntil,
    [property: JsonPropertyName("next_payment_period")] SubscriptionPeriodResponseDto NextPaymentPeriod);

public sealed record SubscriptionPeriodResponseDto(
    [property: JsonPropertyName("start")] DateTimeOffset Start,
    [property: JsonPropertyName("end")] DateTimeOffset End);

public sealed record CreateSubscriptionPaymentResponseDto(
    [property: JsonPropertyName("payment_id")] Guid PaymentId,
    [property: JsonPropertyName("confirmation_url")] string ConfirmationUrl,
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("period_start")] DateTimeOffset PeriodStart,
    [property: JsonPropertyName("period_end")] DateTimeOffset PeriodEnd);

public sealed record GetSubscriptionPaymentResponseDto(
    [property: JsonPropertyName("payment_id")] Guid PaymentId,
    [property: JsonPropertyName("status")] SubscriptionPaymentStatus Status,
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("period_start")] DateTimeOffset PeriodStart,
    [property: JsonPropertyName("period_end")] DateTimeOffset PeriodEnd,
    [property: JsonPropertyName("paid_at")] DateTimeOffset? PaidAt);

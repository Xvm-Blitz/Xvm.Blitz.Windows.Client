using Xvm.Blitz.Windows.Client.Core.Models;

namespace Xvm.Blitz.Windows.Client.Core.Services.Abstractions;

public interface ISubscriptionService
{
    Task<GetSubscriptionPublicPricingResponseDto?> GetPublicPricingAsync(CancellationToken cancellationToken = default);

    Task<GetSubscriptionUserPricingResponseDto?> GetUserPricingAsync(CancellationToken cancellationToken = default);

    Task<CreateSubscriptionPaymentResponseDto?> CreatePaymentAsync(CancellationToken cancellationToken = default);

    Task<GetSubscriptionPaymentResponseDto?> GetPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default);
}

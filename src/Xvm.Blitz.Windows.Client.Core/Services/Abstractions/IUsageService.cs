using Xvm.Blitz.Windows.Client.Core.Models;

namespace Xvm.Blitz.Windows.Client.Core.Services.Abstractions;

public interface IUsageService
{
    Task<GetUsageResponseDto?> Get();
}
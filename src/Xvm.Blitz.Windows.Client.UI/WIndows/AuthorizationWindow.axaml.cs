using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions.Authorization;
using Xvm.Blitz.Windows.Client.UI.ViewModels;

namespace Xvm.Blitz.Windows.Client.UI.Windows;

public partial class AuthorizationWindow : Window
{
    public AuthorizationWindow()
    {
        AvaloniaXamlLoader.Load(this);

        var authorizationService = App.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var quotaService = App.ServiceProvider.GetRequiredService<IUsageService>();
        var logger = App.ServiceProvider.GetRequiredService<ILogger<AuthorizationViewModel>>();

        var viewModel = new AuthorizationViewModel(authorizationService, quotaService, logger);
        DataContext = viewModel;
    }
}
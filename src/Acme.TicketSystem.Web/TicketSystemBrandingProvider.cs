using Microsoft.Extensions.Localization;
using Acme.TicketSystem.Localization;
using Volo.Abp.Ui.Branding;
using Volo.Abp.DependencyInjection;

namespace Acme.TicketSystem.Web;

[Dependency(ReplaceServices = true)]
public class TicketSystemBrandingProvider : DefaultBrandingProvider
{
    private IStringLocalizer<TicketSystemResource> _localizer;

    public TicketSystemBrandingProvider(IStringLocalizer<TicketSystemResource> localizer)
    {
        _localizer = localizer;
    }

    public override string AppName => _localizer["AppName"];
}

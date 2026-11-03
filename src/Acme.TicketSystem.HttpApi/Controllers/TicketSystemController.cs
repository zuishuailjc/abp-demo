using Acme.TicketSystem.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace Acme.TicketSystem.Controllers;

/* Inherit your controllers from this class.
 */
public abstract class TicketSystemController : AbpControllerBase
{
    protected TicketSystemController()
    {
        LocalizationResource = typeof(TicketSystemResource);
    }
}

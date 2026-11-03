using Acme.TicketSystem.Localization;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace Acme.TicketSystem.Web.Pages;

/* Inherit your PageModel classes from this class.
 */
public abstract class TicketSystemPageModel : AbpPageModel
{
    protected TicketSystemPageModel()
    {
        LocalizationResourceType = typeof(TicketSystemResource);
    }
}

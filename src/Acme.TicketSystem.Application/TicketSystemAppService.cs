using System;
using System.Collections.Generic;
using System.Text;
using Acme.TicketSystem.Localization;
using Volo.Abp.Application.Services;

namespace Acme.TicketSystem;

/* Inherit your application services from this class.
 */
public abstract class TicketSystemAppService : ApplicationService
{
    protected TicketSystemAppService()
    {
        LocalizationResource = typeof(TicketSystemResource);
    }
}

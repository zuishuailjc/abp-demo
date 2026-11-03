using Acme.TicketSystem.Permissions;
using Microsoft.AspNetCore.Authorization;

namespace Acme.TicketSystem.Web.Pages.Ledger;

[Authorize(TicketSystemPermissions.Tickets.Default)]
public class IndexModel : TicketSystemPageModel
{
}

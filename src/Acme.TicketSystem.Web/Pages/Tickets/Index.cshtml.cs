using Acme.TicketSystem.Permissions;
using Microsoft.AspNetCore.Authorization;

namespace Acme.TicketSystem.Web.Pages.Tickets;

[Authorize(TicketSystemPermissions.Tickets.Default)]
public class IndexModel : TicketSystemPageModel
{
}

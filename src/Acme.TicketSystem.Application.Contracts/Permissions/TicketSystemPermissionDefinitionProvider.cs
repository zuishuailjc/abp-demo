using Acme.TicketSystem.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace Acme.TicketSystem.Permissions;

public class TicketSystemPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(TicketSystemPermissions.GroupName);

        //售票权限
        var tickets = myGroup.AddPermission(TicketSystemPermissions.Tickets.Default, L("Permission:Tickets"));
        tickets.AddChild(TicketSystemPermissions.Tickets.Sell, L("Permission:Tickets.Sell"));
        tickets.AddChild(TicketSystemPermissions.Tickets.Refund, L("Permission:Tickets.Refund"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<TicketSystemResource>(name);
    }
}

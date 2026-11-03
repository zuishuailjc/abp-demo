using System.Threading.Tasks;
using Acme.TicketSystem.Localization;
using Acme.TicketSystem.MultiTenancy;
using Acme.TicketSystem.Permissions;
using Volo.Abp.Identity.Web.Navigation;
using Volo.Abp.SettingManagement.Web.Navigation;
using Volo.Abp.TenantManagement.Web.Navigation;
using Volo.Abp.UI.Navigation;

namespace Acme.TicketSystem.Web.Menus;

public class TicketSystemMenuContributor : IMenuContributor
{
    public async Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        if (context.Menu.Name == StandardMenus.Main)
        {
            await ConfigureMainMenuAsync(context);
        }
    }

    private Task ConfigureMainMenuAsync(MenuConfigurationContext context)
    {
        var administration = context.Menu.GetAdministration();
        var l = context.GetLocalizer<TicketSystemResource>();

        context.Menu.Items.Insert(
            0,
            new ApplicationMenuItem(
                TicketSystemMenus.Home,
                l["Menu:Home"],
                "~/",
                icon: "fas fa-home",
                order: 0
            )
        );

        context.Menu.Items.Insert(
            1,
            new ApplicationMenuItem(
                TicketSystemMenus.Tickets,
                l["Menu:Tickets"],
                "~/Tickets",
                icon: "fas fa-ticket",
                order: 1,
                requiredPermissionName: TicketSystemPermissions.Tickets.Default
            )
        );

        context.Menu.Items.Insert(
            2,
            new ApplicationMenuItem(
                TicketSystemMenus.Ledger,
                l["Menu:Ledger"],
                "~/Ledger",
                icon: "fas fa-receipt",
                order: 2,
                requiredPermissionName: TicketSystemPermissions.Tickets.Default
            )
        );

        if (MultiTenancyConsts.IsEnabled)
        {
            administration.SetSubItemOrder(TenantManagementMenuNames.GroupName, 1);
        }
        else
        {
            administration.TryRemoveMenuItem(TenantManagementMenuNames.GroupName);
        }

        administration.SetSubItemOrder(IdentityMenuNames.GroupName, 2);
        administration.SetSubItemOrder(SettingManagementMenuNames.GroupName, 3);

        return Task.CompletedTask;
    }
}

using Acme.TicketSystem.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace Acme.TicketSystem.DbMigrator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(TicketSystemEntityFrameworkCoreModule),
    typeof(TicketSystemApplicationContractsModule)
    )]
public class TicketSystemDbMigratorModule : AbpModule
{
}

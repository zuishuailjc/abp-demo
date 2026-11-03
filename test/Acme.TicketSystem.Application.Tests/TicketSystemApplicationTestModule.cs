using Volo.Abp.Modularity;

namespace Acme.TicketSystem;

[DependsOn(
    typeof(TicketSystemApplicationModule),
    typeof(TicketSystemDomainTestModule)
)]
public class TicketSystemApplicationTestModule : AbpModule
{

}

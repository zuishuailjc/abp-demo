using Volo.Abp.Modularity;

namespace Acme.TicketSystem;

[DependsOn(
    typeof(TicketSystemDomainModule),
    typeof(TicketSystemTestBaseModule)
)]
public class TicketSystemDomainTestModule : AbpModule
{

}

using Volo.Abp.Modularity;

namespace Acme.TicketSystem;

public abstract class TicketSystemApplicationTestBase<TStartupModule> : TicketSystemTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}

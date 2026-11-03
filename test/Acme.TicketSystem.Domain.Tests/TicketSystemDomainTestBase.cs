using Volo.Abp.Modularity;

namespace Acme.TicketSystem;

/* Inherit from this class for your domain layer tests. */
public abstract class TicketSystemDomainTestBase<TStartupModule> : TicketSystemTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}

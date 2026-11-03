using Xunit;

namespace Acme.TicketSystem.EntityFrameworkCore;

[CollectionDefinition(TicketSystemTestConsts.CollectionDefinitionName)]
public class TicketSystemEntityFrameworkCoreCollection : ICollectionFixture<TicketSystemEntityFrameworkCoreFixture>
{

}

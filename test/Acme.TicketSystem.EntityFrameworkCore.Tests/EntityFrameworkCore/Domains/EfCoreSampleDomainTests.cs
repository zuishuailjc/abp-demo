using Acme.TicketSystem.Samples;
using Xunit;

namespace Acme.TicketSystem.EntityFrameworkCore.Domains;

[Collection(TicketSystemTestConsts.CollectionDefinitionName)]
public class EfCoreSampleDomainTests : SampleDomainTests<TicketSystemEntityFrameworkCoreTestModule>
{

}

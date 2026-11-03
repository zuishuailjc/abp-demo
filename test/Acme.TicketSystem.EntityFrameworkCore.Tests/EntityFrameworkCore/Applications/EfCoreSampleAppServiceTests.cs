using Acme.TicketSystem.Samples;
using Xunit;

namespace Acme.TicketSystem.EntityFrameworkCore.Applications;

[Collection(TicketSystemTestConsts.CollectionDefinitionName)]
public class EfCoreSampleAppServiceTests : SampleAppServiceTests<TicketSystemEntityFrameworkCoreTestModule>
{

}

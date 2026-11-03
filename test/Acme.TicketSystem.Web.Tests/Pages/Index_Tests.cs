using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace Acme.TicketSystem.Pages;

public class Index_Tests : TicketSystemWebTestBase
{
    [Fact]
    public async Task Welcome_Page()
    {
        var response = await GetResponseAsStringAsync("/");
        response.ShouldNotBeNull();
    }
}

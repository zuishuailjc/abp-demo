using System.Linq;
using System.Threading.Tasks;
using Acme.TicketSystem.EntityFrameworkCore;
using Acme.TicketSystem.Tickets;
using Shouldly;
using Xunit;

namespace Acme.TicketSystem.EntityFrameworkCore.Tickets;

/// <summary>
/// 定价领域服务的集成测试：连真实的 SQLite 内存库，走真实的仓储实现。
/// <para>
/// 和 Domain.Tests 里的纯单元测试互补：
/// 单元测试证明"规则算得对"，这个测试证明"规则接得上数据库、种子数据是齐的"。
/// </para>
/// </summary>
[Collection(TicketSystemTestConsts.CollectionDefinitionName)]
public class TicketPricingEfCoreTests : TicketSystemEntityFrameworkCoreTestBase
{
    private readonly ITicketPriceRepository _ticketPriceRepository;
    private readonly TicketPricingDomainService _ticketPricingDomainService;

    public TicketPricingEfCoreTests()
    {
        _ticketPriceRepository = GetRequiredService<ITicketPriceRepository>();
        _ticketPricingDomainService = GetRequiredService<TicketPricingDomainService>();
    }

    [Fact]
    public async Task 种子数据应当写入票种目录()
    {
        var prices = await _ticketPriceRepository.GetActiveListAsync();

        prices.ShouldNotBeEmpty();
        prices.ShouldContain(x => x.TicketCode == "ADULT");
    }

    [Fact]
    public async Task 领域服务通过真实仓储应当能算出成交价()
    {
        var result = await _ticketPricingDomainService.CalculateAsync(new[]
        {
            new PricingLineRequest("ADULT", 1)
        });

        var quote = result.Quotes.Single();

        quote.UnitPrice.ShouldBe(100m);
        result.PayableAmount.ShouldBe(100m);
    }
}

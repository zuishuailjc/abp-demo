using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Acme.TicketSystem.EntityFrameworkCore;
using Acme.TicketSystem.Tickets;
using Shouldly;
using Xunit;

namespace Acme.TicketSystem.EntityFrameworkCore.Tickets;

/// <summary>
/// 售票用例的集成测试：从"客户端只传票种和张数"开始，
/// 一路走到"票卖出去、账记上、金额对得上"。
/// </summary>
[Collection(TicketSystemTestConsts.CollectionDefinitionName)]
public class TicketSellEfCoreTests : TicketSystemEntityFrameworkCoreTestBase
{
    private readonly ITicketAppService _ticketAppService;

    public TicketSellEfCoreTests()
    {
        _ticketAppService = GetRequiredService<ITicketAppService>();
    }

    [Fact]
    public async Task 客户端不传价格时服务端应当按票种目录定价并记账()
    {
        // 注意入参：只有票种编码和张数，没有单价
        var result = await _ticketAppService.SellAsync(new SellTicketsInput
        {
            StoreId = "S001",
            Lines = new List<TicketLineInput>
            {
                new() { TicketCode = "ADULT", Quantity = 1 }
            }
        });

        // 目录价 100
        result.OriginalAmount.ShouldBe(100m);
        result.TotalAmount.ShouldBe(100m);
        result.DiscountAmount.ShouldBe(0m);

        // 出了一张票
        result.Tickets.Count.ShouldBe(1);
        result.Tickets[0].TicketNo.ShouldNotBeNullOrWhiteSpace();

        // 账上记了两条流水：消费 + 付款
        result.LedgerEntries.Count.ShouldBe(2);

        // 账实一致：流水金额合计 == 售票单应收
        result.LedgerEntries.Sum(x => x.Amount).ShouldBe(result.TotalAmount);
        result.LedgerEntries.Sum(x => x.Credit).ShouldBe(result.TotalAmount);
    }

    [Fact]
    public async Task 达到满减门槛时整单应当优惠并且账仍然对得上()
    {
        var result = await _ticketAppService.SellAsync(new SellTicketsInput
        {
            StoreId = "S001",
            Lines = new List<TicketLineInput>
            {
                new() { TicketCode = "ADULT", Quantity = 3 }
            }
        });

        // 原价 3 × 100 = 300，满 200 减 30，实付 270
        result.OriginalAmount.ShouldBe(300m);
        result.DiscountAmount.ShouldBe(30m);
        result.TotalAmount.ShouldBe(270m);

        // 成交单价是折后的 90
        result.Lines[0].UnitPrice.ShouldBe(90m);

        // 关键：不管怎么打折，账上的金额必须等于收的钱
        result.LedgerEntries.Sum(x => x.Amount).ShouldBe(result.TotalAmount);
        result.LedgerEntries.Sum(x => x.Credit).ShouldBe(result.TotalAmount);
    }
}

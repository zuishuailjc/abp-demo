using System;
using System.Linq;
using Acme.TicketSystem.Tickets;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Acme.TicketSystem.Domains;

/// <summary>
/// 售票聚合的纯领域测试。
/// <para>
/// 这里刻意不继承任何 ABP 测试基类、不连数据库、不走仓储：
/// 聚合根 TicketSale 就是一个普通对象，把业务规则写在自己身上，
/// 所以业务规则可以脱离数据库、脱离框架直接断言。
/// </para>
/// <para>
/// 这正是把业务从"一个 200 行的 Service 方法"拆进聚合之后拿到的收益：
/// 原来这些规则只能靠连库跑整个流程才能验，现在 0.5 秒就能跑完。
/// </para>
/// </summary>
public class TicketSaleTests
{
    private static readonly DateTime SoldAt = new(2026, 11, 3, 10, 30, 0);

    /// <summary>造一张只含一个票种的售票单。</summary>
    private static TicketSale CreateSale(decimal unitPrice = 100m, int quantity = 1)
    {
        var sale = new TicketSale(
            tenantId: null,
            saleNo: "S20261103103000001",
            storeId: "STORE01",
            soldBy: "seller01",
            soldAt: SoldAt,
            customerName: "张三",
            customerPhone: "13800000000");

        sale.AddLine("ADULT", "成人票", unitPrice, quantity);

        return sale;
    }

    [Fact]
    public void 售票后应为每张票生成两条正向流水且合计等于票面金额()
    {
        var sale = CreateSale(unitPrice: 100m, quantity: 2);

        sale.IssueTickets(new[] { "T0001", "T0002" });

        // 两张票 -> 四条流水，每张票两条（消费 + 付款）
        sale.LedgerEntries.Count.ShouldBe(4);

        var firstTicketEntries = sale.LedgerEntries.Where(x => x.TicketNo == "T0001").ToList();
        firstTicketEntries.Count.ShouldBe(2);
        firstTicketEntries.ShouldAllBe(x => x.Quantity == 1);
        firstTicketEntries.ShouldAllBe(x => x.Amount > 0 || x.Credit > 0);

        // 消费码记应收 100，付款码记已收 100，两者不相加、不重复计
        firstTicketEntries.Single(x => x.EntryType == TicketLedgerEntryType.Sale).Amount.ShouldBe(100m);
        firstTicketEntries.Single(x => x.EntryType == TicketLedgerEntryType.Payment).Credit.ShouldBe(100m);

        // 整单流水金额合计 = 应收合计
        sale.LedgerEntries.Sum(x => x.Amount).ShouldBe(sale.TotalAmount);
        sale.TotalAmount.ShouldBe(200m);
    }

    [Fact]
    public void 退票后应生成两条负数流水且冲销金额是原值的相反数()
    {
        var sale = CreateSale(unitPrice: 100m);
        sale.IssueTickets(new[] { "T0001" });

        sale.RefundTicket("T0001", reason: "客户临时取消", refundedAt: SoldAt.AddHours(1));

        var refundEntries = sale.LedgerEntries
            .Where(x => x.EntryType == TicketLedgerEntryType.RefundSale
                     || x.EntryType == TicketLedgerEntryType.RefundPayment)
            .ToList();

        refundEntries.Count.ShouldBe(2);

        var originalSale = sale.LedgerEntries.Single(x => x.EntryType == TicketLedgerEntryType.Sale);
        var originalPayment = sale.LedgerEntries.Single(x => x.EntryType == TicketLedgerEntryType.Payment);

        var refundSale = refundEntries.Single(x => x.EntryType == TicketLedgerEntryType.RefundSale);
        var refundPayment = refundEntries.Single(x => x.EntryType == TicketLedgerEntryType.RefundPayment);

        // 冲销金额就是原值的相反数
        refundSale.Amount.ShouldBe(-originalSale.Amount);
        refundSale.Credit.ShouldBe(-originalSale.Credit);
        refundPayment.Amount.ShouldBe(-originalPayment.Amount);
        refundPayment.Credit.ShouldBe(-originalPayment.Credit);

        refundSale.Amount.ShouldBe(-100m);
        refundPayment.Credit.ShouldBe(-100m);
    }

    [Fact]
    public void 同一张票退第二次应抛出TicketAlreadyRefunded()
    {
        var sale = CreateSale(unitPrice: 100m);
        sale.IssueTickets(new[] { "T0001" });

        sale.RefundTicket("T0001", reason: "第一次退票", refundedAt: SoldAt.AddHours(1));

        var ex = Should.Throw<BusinessException>(
            () => sale.RefundTicket("T0001", reason: "重复退票", refundedAt: SoldAt.AddHours(2)));

        ex.Code.ShouldBe(TicketSystemDomainErrorCodes.TicketAlreadyRefunded);

        // 重复退票不应该再产生新的冲销流水
        sale.LedgerEntries.Count.ShouldBe(4);
    }

    [Fact]
    public void 一张票全生命周期流水金额合计应为零()
    {
        var sale = CreateSale(unitPrice: 100m);
        sale.IssueTickets(new[] { "T0001" });

        // 售出
        sale.LedgerEntries.Sum(x => x.Amount).ShouldBe(100m);

        // 退票
        sale.RefundTicket("T0001", reason: "客户临时取消", refundedAt: SoldAt.AddHours(1));

        // 应收冲回为 0，已收冲回为 0 —— 账平了
        sale.LedgerEntries.Sum(x => x.Amount).ShouldBe(0m);
        sale.LedgerEntries.Sum(x => x.Credit).ShouldBe(0m);

        // 数量也是正负相抵
        sale.LedgerEntries.Sum(x => x.Quantity).ShouldBe(0);

        sale.RefundedAmount.ShouldBe(100m);
        sale.IsFullyRefunded.ShouldBeTrue();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace Acme.TicketSystem.Tickets;

/// <summary>
/// 定价领域服务。
/// <para>
/// <strong>它是干什么的：</strong>回答"这一单应该收多少钱"。
/// </para>
/// <para>
/// <strong>为什么它是领域服务，而不是聚合里的方法：</strong>
/// 定价需要读三样<strong>不属于这张售票单</strong>的东西——
/// <list type="number">
///   <item>票种目录（TicketPrice 聚合）：这个票种标价多少；</item>
///   <item>优惠活动（DiscountPolicy）：当前满多少减多少；</item>
///   <item>客户端给的"我要买什么、买几张"。</item>
/// </list>
/// 这些规则没有主人：它们不属于 TicketSale（一张售票单不携带目录和活动规则），
/// 也不属于 TicketPrice（目录不知道自己会不会被优惠）。
/// 没有主人的领域逻辑，就是领域服务的定义。
/// </para>
/// <para>
/// <strong>为什么不放到应用服务里：</strong>应用服务（TicketAppService）负责编排用例，
/// 不该包含业务规则。定价规则是业务规则，放应用服务里就等于把规则和流程搅在一起，
/// 而且规则没法被单独测试。
/// </para>
/// <para>
/// <strong>领域服务的三条纪律：</strong>
/// <list type="number">
///   <item><strong>无状态</strong>：不保存任何业务数据，每次调用都从外部取当前值；</item>
///   <item><strong>不做持久化</strong>：只读聚合，不保存聚合（写状态一律回聚合根）；</item>
///   <item><strong>不做 DTO 转换</strong>：进出都是领域对象，DTO 是应用层的事。</item>
/// </list>
/// </para>
/// </summary>
public class TicketPricingDomainService : DomainService
{
    private readonly ITicketPriceRepository _ticketPriceRepository;

    public TicketPricingDomainService(ITicketPriceRepository ticketPriceRepository)
    {
        _ticketPriceRepository = ticketPriceRepository;
    }

    /// <summary>
    /// 列出在售的票种目录，给页面上拉框用。
    /// </summary>
    public async Task<List<TicketPrice>> GetActivePricesAsync(CancellationToken cancellationToken = default)
    {
        return await _ticketPriceRepository.GetActiveListAsync(cancellationToken);
    }

    /// <summary>
    /// 给一批票种行定价：查目录价 → 算原价 → 应用整单优惠 → 把优惠摊到各行。
    /// </summary>
    /// <param name="lines">客户要买的票种和张数（不含价格）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>每一行的成交单价和整单金额。</returns>
    public async Task<TicketPricingResult> CalculateAsync(
        IReadOnlyList<PricingLineRequest> lines,
        CancellationToken cancellationToken = default)
    {
        Check.NotNull(lines, nameof(lines));

        if (lines.Count == 0)
        {
            throw new BusinessException(TicketSystemDomainErrorCodes.TicketLinesCannotBeEmpty);
        }

        // ---- 第一步：查目录价 ----
        var quotes = new List<TicketPriceQuote>(lines.Count);

        foreach (var line in lines)
        {
            if (line.Quantity <= 0)
            {
                throw new BusinessException(TicketSystemDomainErrorCodes.TicketQuantityMustBePositive)
                    .WithData("TicketCode", line.TicketCode);
            }

            var price = await _ticketPriceRepository.FindByCodeAsync(line.TicketCode, cancellationToken);

            if (price == null || !price.IsActive)
            {
                // 票种不存在或已停售——不能卖。这里抛业务异常，
                // 而不是像老代码那样 catch 一下打印日志然后继续往下写。
                throw new BusinessException(TicketSystemDomainErrorCodes.TicketPriceNotFound)
                    .WithData("TicketCode", line.TicketCode);
            }

            quotes.Add(new TicketPriceQuote(
                price.TicketCode,
                price.TicketName,
                price.UnitPrice,
                price.UnitPrice,
                line.Quantity));
        }

        // ---- 第二步：整单优惠 ----
        var originalAmount = quotes.Sum(x => x.OriginalAmount);
        var discount = DiscountPolicy.Current.GetDiscount(originalAmount);

        if (discount <= 0m)
        {
            return new TicketPricingResult(quotes, 0m);
        }

        // ---- 第三步：把优惠摊到各行 ----
        // 按各行原价金额的比例摊分，最后一行补差，保证"摊出去的优惠之和 == 整单优惠"。
        var allocated = AllocateDiscount(quotes, discount);

        var finalQuotes = new List<TicketPriceQuote>(quotes.Count);
        for (var i = 0; i < quotes.Count; i++)
        {
            var quote = quotes[i];
            var lineDiscount = allocated[i];

            // 成交单价 = (原价小计 - 本行优惠) / 张数，保留两位小数。
            // 应付金额以"最终单价 × 张数"为准，所以聚合记的账和这里算的钱一定对得上。
            var unitPrice = Math.Round((quote.OriginalAmount - lineDiscount) / quote.Quantity, 2, MidpointRounding.AwayFromZero);

            finalQuotes.Add(new TicketPriceQuote(
                quote.TicketCode,
                quote.TicketName,
                quote.OriginalUnitPrice,
                unitPrice,
                quote.Quantity));
        }

        // 实际优惠 = 原价合计 - 最终应付（以最终单价为准，避免分位误差）
        var actualDiscount = finalQuotes.Sum(x => x.OriginalAmount) - finalQuotes.Sum(x => x.Amount);

        return new TicketPricingResult(finalQuotes, actualDiscount);
    }

    /// <summary>
    /// 把整单优惠按各行原价金额的比例摊到各行。
    /// 返回每一行摊到的优惠金额，合计等于传入的整单优惠。
    /// </summary>
    private static List<decimal> AllocateDiscount(IReadOnlyList<TicketPriceQuote> quotes, decimal discount)
    {
        var result = new List<decimal>(quotes.Count);

        var total = quotes.Sum(x => x.OriginalAmount);
        var allocated = 0m;

        for (var i = 0; i < quotes.Count; i++)
        {
            var quote = quotes[i];

            decimal share;
            if (i == quotes.Count - 1)
            {
                // 最后一行补差，保证分摊总和精确等于整单优惠
                share = discount - allocated;
            }
            else
            {
                share = Math.Round(discount * quote.OriginalAmount / total, 2, MidpointRounding.AwayFromZero);
                allocated += share;
            }

            // 单行优惠不能超过它自己的原价
            result.Add(Math.Min(share, quote.OriginalAmount));
        }

        return result;
    }
}

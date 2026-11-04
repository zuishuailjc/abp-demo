using System;
using System.Collections.Generic;
using System.Linq;

namespace Acme.TicketSystem.Tickets;

/// <summary>
/// 一个票种的报价结果（领域层的值对象，不是 DTO）。
/// <para>
/// 它描述"这个票种最终卖多少钱"：目录原价是多少、摊到它头上的优惠是多少、
/// 最后实际成交单价是多少。成交单价是应用服务写进聚合的唯一价格来源。
/// </para>
/// </summary>
public class TicketPriceQuote
{
    /// <summary>票种编码。</summary>
    public string TicketCode { get; }

    /// <summary>票种名称（来自票种目录，不由客户端传）。</summary>
    public string TicketName { get; }

    /// <summary>目录原价（票种目录里的单价）。</summary>
    public decimal OriginalUnitPrice { get; }

    /// <summary>实际成交单价 = 原价 - 摊到本行上的单位优惠。</summary>
    public decimal UnitPrice { get; }

    /// <summary>购买张数。</summary>
    public int Quantity { get; }

    /// <summary>本行小计 = 成交单价 × 张数。</summary>
    public decimal Amount => UnitPrice * Quantity;

    /// <summary>本行原价小计。</summary>
    public decimal OriginalAmount => OriginalUnitPrice * Quantity;

    public TicketPriceQuote(
        string ticketCode,
        string ticketName,
        decimal originalUnitPrice,
        decimal unitPrice,
        int quantity)
    {
        TicketCode = ticketCode;
        TicketName = ticketName;
        OriginalUnitPrice = originalUnitPrice;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }
}

/// <summary>
/// 一次定价的完整结果（领域层的值对象）。
/// <para>
/// 注意这里的金额关系，这是整个定价逻辑要守住的账：
/// <c>PayableAmount == Quotes.Sum(q =&gt; q.Amount)</c>，
/// 而聚合的应收合计又是 <c>Σ(成交单价 × 张数)</c>，
/// 所以"卖多少钱"和"记多少钱"从源头就是同一个数，不会出现账实不符。
/// </para>
/// </summary>
public class TicketPricingResult
{
    /// <summary>每一行的报价。</summary>
    public IReadOnlyList<TicketPriceQuote> Quotes { get; }

    /// <summary>原价合计（不打折时的总额）。</summary>
    public decimal OriginalAmount => Quotes.Sum(x => x.OriginalAmount);

    /// <summary>整单优惠金额（实际生效的）。</summary>
    public decimal DiscountAmount { get; }

    /// <summary>应付金额（实际要收的钱）。</summary>
    public decimal PayableAmount => Quotes.Sum(x => x.Amount);

    public TicketPricingResult(IReadOnlyList<TicketPriceQuote> quotes, decimal discountAmount)
    {
        Quotes = quotes;
        DiscountAmount = discountAmount;
    }
}

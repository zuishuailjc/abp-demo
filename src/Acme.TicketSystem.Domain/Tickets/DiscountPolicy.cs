using System;

namespace Acme.TicketSystem.Tickets;

/// <summary>
/// 整单优惠规则（值对象）：满多少减多少。
/// <para>
/// 这条规则不属于任何一张售票单——它是"当前的活动政策"，
/// 一张卖出去的票不会带着这条规则（票上记的是成交价，不是活动规则）。
/// 所以它做成一个独立的值对象，由领域服务在定价时读取。
/// </para>
/// </summary>
public class DiscountPolicy
{
    /// <summary>门槛：原价合计达到多少才享受优惠。</summary>
    public decimal Threshold { get; }

    /// <summary>减免金额。</summary>
    public decimal Reduction { get; }

    /// <summary>是否启用。</summary>
    public bool IsActive { get; }

    private DiscountPolicy(decimal threshold, decimal reduction, bool isActive)
    {
        Threshold = threshold;
        Reduction = reduction;
        IsActive = isActive;
    }

    /// <summary>当前生效的活动：满 200 减 30。</summary>
    public static DiscountPolicy Current { get; } = new(200m, 30m, true);

    /// <summary>
    /// 按原价合计算出整单优惠。没达到门槛返回 0。
    /// </summary>
    public decimal GetDiscount(decimal originalAmount)
    {
        if (!IsActive || originalAmount < Threshold)
        {
            return 0m;
        }

        // 优惠不会超过商品本身
        return Math.Min(Reduction, originalAmount);
    }
}

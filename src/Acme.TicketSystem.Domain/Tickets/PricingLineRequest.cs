namespace Acme.TicketSystem.Tickets;

/// <summary>
/// 定价请求的一行：客户要买什么票、买几张。
/// <para>
/// 注意这里<strong>没有单价</strong>——单价由服务端根据票种目录算出来。
/// 客户端只负责说"我要哪个票、几张"，无权定价。
/// </para>
/// </summary>
public class PricingLineRequest
{
    /// <summary>票种编码。</summary>
    public string TicketCode { get; }

    /// <summary>张数。</summary>
    public int Quantity { get; }

    public PricingLineRequest(string ticketCode, int quantity)
    {
        TicketCode = ticketCode;
        Quantity = quantity;
    }
}

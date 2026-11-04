using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Acme.TicketSystem.Tickets;

/// <summary>
/// 窗口售票入参。对应原项目 Sale(IList&lt;WindowsTicket&gt; ...) 的参数，
/// 但只保留售票必需字段，导游/司机/车牌这些噪音去掉了。
/// </summary>
public class SellTicketsInput
{
    /// <summary>门店编码。</summary>
    [Required]
    [StringLength(TicketConsts.MaxStoreIdLength)]
    public string StoreId { get; set; } = string.Empty;

    [StringLength(TicketConsts.MaxCustomerNameLength)]
    public string? CustomerName { get; set; }

    [StringLength(TicketConsts.MaxCustomerPhoneLength)]
    public string? CustomerPhone { get; set; }

    /// <summary>要买的票种，可以一次买多种。</summary>
    [Required]
    public List<TicketLineInput> Lines { get; set; } = new();
}

/// <summary>
/// 一个票种 + 张数。
/// <para>
/// <strong>这里没有单价，也没有票种名称</strong>——两者都由服务端根据票种目录决定。
/// 客户端只负责说"我要哪个票、买几张"，无权定价。
/// 这也是为什么定价逻辑属于领域服务而不是聚合方法：价格来自这张单之外。
/// </para>
/// </summary>
public class TicketLineInput
{
    [Required]
    [StringLength(TicketConsts.MaxTicketCodeLength)]
    public string TicketCode { get; set; } = string.Empty;

    [Range(1, 999)]
    public int Quantity { get; set; }
}

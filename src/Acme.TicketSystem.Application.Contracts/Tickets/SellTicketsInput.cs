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

/// <summary>一个票种 + 单价 + 张数。</summary>
public class TicketLineInput
{
    [Required]
    [StringLength(TicketConsts.MaxTicketCodeLength)]
    public string TicketCode { get; set; } = string.Empty;

    [Required]
    [StringLength(TicketConsts.MaxTicketNameLength)]
    public string TicketName { get; set; } = string.Empty;

    [Range(0, 9999999)]
    public decimal UnitPrice { get; set; }

    [Range(1, 999)]
    public int Quantity { get; set; }
}

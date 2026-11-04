using System;
using System.Collections.Generic;

namespace Acme.TicketSystem.Tickets;

/// <summary>售票结果，前端拿 Tickets 逐个打印二维码。</summary>
public class TicketSaleDto
{
	public Guid Id { get; set; }
	public string SaleNo { get; set; } = string.Empty;
	public string StoreId { get; set; } = string.Empty;
	public string SoldBy { get; set; } = string.Empty;
	public DateTime SoldAt { get; set; }
	public string? CustomerName { get; set; }
	public string? CustomerPhone { get; set; }
	public decimal TotalAmount { get; set; }
	public int TotalQuantity { get; set; }

	/// <summary>
	/// 原价合计（未打折前）。定价是领域服务的事，聚合自己不知道原价，
	/// 所以只有售票响应里有值，退票/核销响应为 0。
	/// </summary>
	public decimal OriginalAmount { get; set; }

	/// <summary>整单优惠金额。同样只有售票响应里有值。</summary>
	public decimal DiscountAmount { get; set; }

	public decimal RefundedAmount { get; set; }
	public bool IsFullyRefunded { get; set; }
	public bool IsPartiallyRefunded { get; set; }

	public List<TicketSaleLineDto> Lines { get; set; } = new();
	public List<SoldTicketDto> Tickets { get; set; } = new();

	public List<TicketLedgerEntryDto> LedgerEntries { get; set; } = new();
}

public class TicketSaleLineDto
{
	public string TicketCode { get; set; } = string.Empty;
	public string TicketName { get; set; } = string.Empty;
	public decimal UnitPrice { get; set; }
	public int Quantity { get; set; }
	public decimal Amount { get; set; }
}

public class SoldTicketDto
{
	public string TicketNo { get; set; } = string.Empty;
	public string TicketCode { get; set; } = string.Empty;
	public string TicketName { get; set; } = string.Empty;
	public decimal Amount { get; set; }
	public bool IsRefunded { get; set; }
	public DateTime? RefundedAt { get; set; }
	public string? RefundReason { get; set; }
	public bool IsValidated { get; set; }
	public DateTime? ValidatedAt { get; set; }
}


public class TicketLedgerEntryDto
{
	public string TicketNo { get; set; } = string.Empty;
	public string EntryType { get; set; } = string.Empty;
	public int Quantity { get; set; }
	public decimal Amount { get; set; }
	public decimal Balance { get; set; }
	public decimal Credit { get; set; }
	public DateTime CreatedAt { get; set; }
}
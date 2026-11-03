using System;
using Volo.Abp.Domain.Entities;

namespace Acme.TicketSystem.Tickets;
///	<summary>	
///	
/// 财务流水 聚合内实体 对应技术性分层的 tb_Account的一条记录
/// 金额字段带符合 正数是销售 负数是冲销
/// </summary>
/// 
public class TicketLedgerEntry : Entity<Guid>
{
	public Guid TicketSaleId { get; private set; }
	public string TicketNo { get; private set; } = string.Empty;

	public TicketLedgerEntryType EntryType { get; private set; }

	public int Quantity { get; private set; }

	///	<summary> 原系统 Charge 销售为正 冲销为负  </summary>	
	public decimal Amount { get; private set; }

	///	<summary> 原系统 Balance 应付款 </summary>
	///	
	public decimal Balance { get; private set; }
	///	<summary> 原系统 Credit 已付款 </summary>
	public decimal Credit { get; private set; }

	public string? Reason { get; private set; }
	public DateTime CreatedAt { get; private set; }

	private TicketLedgerEntry()
	{
		//EF Core
	}

	internal TicketLedgerEntry(
		Guid id,
		Guid ticketSaleId,
		string ticketNo,
		TicketLedgerEntryType entryType,
		int quantity,
		decimal amount,
		decimal balance,
		decimal credit,
		string? reason,
		DateTime createdAt
	) : base(id)
	{
		TicketSaleId = ticketSaleId;
		TicketNo = ticketNo;
		EntryType = entryType;
		Quantity = quantity;
		Amount = amount;
		Balance = balance;
		Credit = credit;
		Reason = reason;
		CreatedAt = createdAt;
	}

	/// <summary> 
	///  冲销 所有数值取反 类型换成对应的退款类型
	///  对应原系统里面 GetRefundCharge / GetRefundCredit 那一串
	/// </summary>	
	internal TicketLedgerEntry Negate(DateTime createAt)
	{
		var refundType = this.EntryType switch
		{

			TicketLedgerEntryType.Sale => TicketLedgerEntryType.RefundSale,
			TicketLedgerEntryType.Payment => TicketLedgerEntryType.RefundPayment,
			_ => throw new NotSupportedException($"不支持的流水类型 {EntryType}")
		};

		return new TicketLedgerEntry(
			Guid.NewGuid(),
			TicketSaleId,
			TicketNo,
			refundType,
			-Quantity,
			-Amount,
			-Balance,
			-Credit,
			Reason,
			createAt
		);
	}
}
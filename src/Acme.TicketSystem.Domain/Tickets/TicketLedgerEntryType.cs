namespace Acme.TicketSystem.Tickets;

/// <summary>
/// 财务流水类型。数值刻意和原系统 tb_Account.PkNumber 保持一致，方便对账。
///</summary>

public enum TicketLedgerEntryType : byte
{
	/// <summary> 消费码 售票 </summary>

	Sale = 1,
	/// <summary> 付款码  收费
	/// </summary>
	Payment = 2,
	/// <summary>
	/// 冲销消费 退票
	/// </summary>
	RefundSale = 3,
	/// <summary>
	///  冲销付款  退票
	/// </summary>
	RefundPayment = 4
}
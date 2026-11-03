namespace Acme.TicketSystem;

public static class TicketSystemDomainErrorCodes
{
	/* 业务异常错误码。命名空间 "TicketSystem" 已在 TicketSystemDomainSharedModule 中
     * 通过 MapCodeNamespace 映射到 TicketSystemResource，文案写在本地化 json 里即可。 */

	public const string OrderNoAlreadyExists = "TicketSystem:00001";

	/// <summary>非草稿订单不能确认</summary>
	public const string OrderCannotConfirm = "TicketSystem:00002";

	/// <summary>已完成订单不能取消</summary>
	public const string OrderCannotCancel = "TicketSystem:00003";

	/// <summary>不是已发货状态，不能完成</summary>
	public const string OrderCannotComplete = "TicketSystem:00004";

	/// <summary>售票：票种行不能为空</summary>
	public const string TicketLinesCannotBeEmpty = "TicketSystem:00101";

	/// <summary>售票：购买张数必须大于 0</summary>
	public const string TicketQuantityMustBePositive = "TicketSystem:00102";

	/// <summary>售票：票种单价不能为负数</summary>
	public const string TicketPriceCannotBeNegative = "TicketSystem:00103";

	/// <summary>售票：票号数量与订单张数不一致</summary>
	public const string TicketNumberCountMismatch = "TicketSystem:00104";

	/// <summary>退票：票号不存在</summary>
	public const string TicketNotFound = "TicketSystem:00105";

	/// <summary>退票：该票已经退过</summary>
	public const string TicketAlreadyRefunded = "TicketSystem:00106";

	/// <summary>核销：已退票的票不能核销</summary>
	public const string RefundedTicketCannotBeValidated = "TicketSystem:00107";

	/// <summary>退款冲销：找不到该票的原始账务流水</summary>
	public const string LedgerEntryNotFound = "TicketSystem:00201";

	/// <summary>退款冲销 该票已经冲销过</summary>
	public const string LedgerAlreadyReversed = "TicketSystem:00202";
}

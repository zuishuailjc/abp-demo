using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Acme.TicketSystem.Tickets;

/// <summary>
/// 售票聚合根：一次窗口售票 = 一个聚合 = 一次事务边界。
/// <para>
/// 对照原项目 TicketService.Sale(...)：那个方法把校验、取号、写 Main/TicketNO/Account 四张表、
/// 组装出票结果全塞在 200 行里，失败只 catch 打印日志（部分表已写入）。
/// 这里拆成三块职责：
/// </para>
/// <list type="bullet">
///   <item>聚合自身：业务规则（张数、单价、票号数量）+ 内部状态一致性；</item>
///   <item>ITicketNumberGenerator：外部资源（号段），由应用服务编排；</item>
///   <item>仓储 SaveChanges：一次落库，天然一个事务。</item>
/// </list>
/// </summary>
public class TicketSale : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }

	/// <summary>售票单号，例如 S20261030153000001。</summary>
	public string SaleNo { get; private set; } = string.Empty;

	/// <summary>门店。</summary>
	public string StoreId { get; private set; } = string.Empty;

	/// <summary>售票员。</summary>
	public string SoldBy { get; private set; } = string.Empty;

	/// <summary>售票时间。</summary>
	public DateTime SoldAt { get; private set; }

	public string? CustomerName { get; private set; }

	public string? CustomerPhone { get; private set; }

	private readonly List<TicketSaleLine> _lines = new();

	/// <summary>票种明细。对外只读，外部不能直接 Add/Remove。</summary>
	public IReadOnlyCollection<TicketSaleLine> Lines => _lines;

	private readonly List<SoldTicket> _tickets = new();

	private readonly List<TicketLedgerEntry> _ledgerEntries = new();
	/// <summary>财务流水。对应原系统 tb_Account的4种记录 </summary>

	public IReadOnlyCollection<TicketLedgerEntry> LedgerEntries => _ledgerEntries;

	/// <summary>已出的票，每张一个票号。前端拿它去打二维码。</summary>
	public IReadOnlyCollection<SoldTicket> Tickets => _tickets;

	/// <summary>应收合计。计算属性，不落库。</summary>
	public decimal TotalAmount => _lines.Sum(x => x.Amount);

	/// <summary>总张数 = 要出多少张票。计算属性，不落库。</summary>
	public int TotalQuantity => _lines.Sum(x => x.Quantity);

	/// <summary>已退金额。计算属性，不落库。</summary>
	public decimal RefundedAmount => _tickets.Where(x => x.IsRefunded).Sum(x => x.Amount);

	/// <summary>整单是否已全部退票。</summary>
	public bool IsFullyRefunded => _tickets.Count > 0 && _tickets.All(x => x.IsRefunded);

	/// <summary>是否部分退票（有些票退了、有些没退）。</summary>
	public bool IsPartiallyRefunded => _tickets.Any(x => x.IsRefunded) && !IsFullyRefunded;

	private TicketSale()
	{
		// EF Core
	}

	public TicketSale(
		Guid? tenantId,
		string saleNo,
		string storeId,
		string soldBy,
		DateTime soldAt,
		string? customerName = null,
		string? customerPhone = null)
	{
		TenantId = tenantId;
		SaleNo = Check.NotNullOrWhiteSpace(saleNo, nameof(saleNo), TicketConsts.MaxSaleNoLength);
		StoreId = Check.NotNullOrWhiteSpace(storeId, nameof(storeId), TicketConsts.MaxStoreIdLength);
		SoldBy = Check.NotNullOrWhiteSpace(soldBy, nameof(soldBy), TicketConsts.MaxSoldByLength);
		SoldAt = soldAt;

		// 原项目用 ref string Msg 返回"客户名称已超过50个字符"，
		// 这里直接在构造时拒绝，调用方拿到的就是异常而不是一个空列表。
		CustomerName = Check.Length(customerName, nameof(customerName), TicketConsts.MaxCustomerNameLength);
		CustomerPhone = Check.Length(customerPhone, nameof(customerPhone), TicketConsts.MaxCustomerPhoneLength);
	}

	/// <summary>
	/// 加一个票种。校验规则就是原项目 CheckInputTicket 里散落的那几条，
	/// 区别是：不满足就直接抛，不再靠返回值传错误消息。
	/// </summary>
	public TicketSaleLine AddLine(string ticketCode, string ticketName, decimal unitPrice, int quantity)
	{
		Check.NotNullOrWhiteSpace(ticketCode, nameof(ticketCode), TicketConsts.MaxTicketCodeLength);
		Check.NotNullOrWhiteSpace(ticketName, nameof(ticketName), TicketConsts.MaxTicketNameLength);

		if (quantity <= 0)
		{
			throw new BusinessException(TicketSystemDomainErrorCodes.TicketQuantityMustBePositive)
				.WithData("TicketCode", ticketCode);
		}

		if (unitPrice < 0)
		{
			throw new BusinessException(TicketSystemDomainErrorCodes.TicketPriceCannotBeNegative)
				.WithData("TicketCode", ticketCode);
		}

		var line = new TicketSaleLine(Guid.NewGuid(), Id, ticketCode, ticketName, unitPrice, quantity);
		_lines.Add(line);
		return line;
	}

	/// <summary>
	/// 出票：把外部生成的票号按明细行的张数依次分配给每一张票。
	/// 票号从哪来（数据库函数、Redis 号段、雪花算法）聚合并不关心。
	/// </summary>
	public void IssueTickets(IReadOnlyList<string> ticketNumbers)
	{
		Check.NotNull(ticketNumbers, nameof(ticketNumbers));

		if (_lines.Count == 0)
		{
			throw new BusinessException(TicketSystemDomainErrorCodes.TicketLinesCannotBeEmpty);
		}

		if (ticketNumbers.Count != TotalQuantity)
		{
			throw new BusinessException(TicketSystemDomainErrorCodes.TicketNumberCountMismatch)
				.WithData("Expected", TotalQuantity)
				.WithData("Actual", ticketNumbers.Count);
		}

		_tickets.Clear();

		var index = 0;
		foreach (var line in _lines)
		{
			for (var i = 0; i < line.Quantity; i++)
			{
				var ticketNo = ticketNumbers[index++];
				_tickets.Add(new SoldTicket(
					Guid.NewGuid(),
					Id,
					ticketNo,
					line.TicketCode,
					line.TicketName,
					line.UnitPrice));

				// 1= 消费码 对应原系统的 GetChargeAccnt
				_ledgerEntries.Add(new TicketLedgerEntry(
					Guid.NewGuid(),
					Id,
					ticketNo,
					TicketLedgerEntryType.Sale,
					quantity: 1,
					amount: line.UnitPrice,
					balance: line.UnitPrice,
					credit: 0,
					reason: null,
					createdAt: SoldAt
				));
				//2 付款码 对应原系统 GetCreditAccnt
				_ledgerEntries.Add(
					new TicketLedgerEntry(
						Guid.NewGuid(),
						Id,
						ticketNo,
						TicketLedgerEntryType.Payment,
						quantity: 1,
						amount: 0,
						balance: 0,
						credit: line.UnitPrice,
						reason: null,
						createdAt: SoldAt
					)
				);
			}
		}
	}

	/// <summary>
	/// 按票号退一张票。原项目 Refund(...) 是按票号退，一次退一张，
	/// 所以这里也是"整单里挑一张退"，而不是整单全退。
	/// 整单状态（全退/部分退）由聚合自己算，不需要额外的状态字段。
	/// </summary>
	public SoldTicket RefundTicket(string ticketNo, string? reason, DateTime refundedAt)
	{
		Check.NotNullOrWhiteSpace(ticketNo, nameof(ticketNo));

		var ticket = _tickets.FirstOrDefault(x => x.TicketNo == ticketNo)
					 ?? throw new BusinessException(TicketSystemDomainErrorCodes.TicketNotFound)
						 .WithData("TicketNo", ticketNo);

		// 先看票本身的状态：这张票退过了，就不要再动账。
		// （放在账务校验之前，是为了让"重复退票"对外报的是票的错，
		//   而不是"账已冲销"的错——同一件事，两个错误码会让人误解。）
		if (ticket.IsRefunded)
		{
			throw new BusinessException(TicketSystemDomainErrorCodes.TicketAlreadyRefunded)
				.WithData("TicketNo", ticketNo);
		}

		#region 新增的财务流水：退款的两条记录，和出票时的两条记录正好相反
		var originals = _ledgerEntries.Where(x => x.TicketNo == ticketNo && (x.EntryType == TicketLedgerEntryType.Sale || x.EntryType == TicketLedgerEntryType.Payment)).ToList();

		if (originals.Count != 2)
		{
			throw new BusinessException(TicketSystemDomainErrorCodes.LedgerEntryNotFound)
				.WithData("TicketNo", ticketNo);
		}

		// 幂等保护 已经冲销过就不再生成第二条
		if (_ledgerEntries.Any(x => x.TicketNo == ticketNo && x.EntryType == TicketLedgerEntryType.RefundSale))
		{
			throw new BusinessException(TicketSystemDomainErrorCodes.LedgerAlreadyReversed).WithData("TicketNo", ticketNo);
		}

		// 票标记已退 (内部还会进行判断拦截重复退票)
		ticket.Refund(reason, refundedAt);
		// 冲销 原流水复制后所有数值取负
		foreach (var entry in originals)
		{
			_ledgerEntries.Add(entry.Negate(refundedAt));
		}
		#endregion

		return ticket;
	}

	/// <summary>
	/// 核销一张票。首次核销会发布 TicketValidatedEvent，
	/// 由订阅方（如将来的渠道对接）处理；聚合本身不认识任何外部系统。
	/// </summary>
	public SoldTicket ValidateTicket(string ticketNo, DateTime validatedAt)
	{
		Check.NotNullOrWhiteSpace(ticketNo, nameof(ticketNo));

		var ticket = _tickets.FirstOrDefault(x => x.TicketNo == ticketNo)
					 ?? throw new BusinessException(TicketSystemDomainErrorCodes.TicketNotFound)
						 .WithData("TicketNo", ticketNo);

		if (ticket.Validate(validatedAt))
		{
			AddLocalEvent(new TicketValidatedEvent(Id, ticketNo, validatedAt));
		}

		return ticket;
	}
}

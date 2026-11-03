# 退票账务冲销 · 手抄步骤

> 照着从上往下抄，每步写完就编译一次。
> 全程只改 `Tickets` 模块。

---

## 抄之前

```bash
# 建个分支，抄错了能直接扔掉
git checkout -b practice/refund-ledger

# 确认基线是绿的
dotnet build src/Acme.TicketSystem.Domain
```

整个练习一共动 **8 个文件**（4 新建、4 修改），顺序如下：

| 步骤 | 文件 | 新建/修改 |
|---|---|---|
| 1 | `Domain.Shared/TicketSystemDomainErrorCodes.cs` | 修改 |
| 2 | `Domain.Shared/Localization/TicketSystem/zh-Hans.json` | 修改 |
| 3 | `Domain.Shared/Localization/TicketSystem/en.json` | 修改 |
| 4 | `Domain/Tickets/TicketLedgerEntryType.cs` | **新建** |
| 5 | `Domain/Tickets/TicketLedgerEntry.cs` | **新建** |
| 6 | `Domain/Tickets/TicketSale.cs` | 修改 |
| 7 | `Application.Contracts/Tickets/TicketSaleDto.cs` | 修改 |
| 8 | `EntityFrameworkCore/EntityFrameworkCore/TicketSystemDbContext.cs` | 修改 |

然后生成迁移、跑 DbMigrator、页面验证。

---

## Step 1 · 加两个错误码

**文件**：`src/Acme.TicketSystem.Domain.Shared/TicketSystemDomainErrorCodes.cs`
**操作**：在类末尾（`TicketAlreadyRefunded` 之后）追加

```csharp
    /// <summary>退款冲销：找不到该票的原始账务流水</summary>
    public const string LedgerEntryNotFound = "TicketSystem:00201";

    /// <summary>退款冲销：该票已经冲销过</summary>
    public const string LedgerAlreadyReversed = "TicketSystem:00202";
```

**验证**：编译

```bash
dotnet build src/Acme.TicketSystem.Domain.Shared
```

---

## Step 2 · 中文文案

**文件**：`src/Acme.TicketSystem.Domain.Shared/Localization/TicketSystem/zh-Hans.json`
**操作**：在 `"texts"` 里追加两行（注意上一行结尾要加逗号）

```json
    "TicketSystem:00201": "找不到该票的原始账务流水，无法冲销。",
    "TicketSystem:00202": "该票已经冲销过，不能重复冲销。"
```

---

## Step 3 · 英文文案

**文件**：`src/Acme.TicketSystem.Domain.Shared/Localization/TicketSystem/en.json`
**操作**：同上

```json
    "TicketSystem:00201": "Original ledger entries not found, cannot reverse.",
    "TicketSystem:00202": "This ticket has already been reversed."
```

**验证**：JSON 别写坏了

```bash
python -c "import json;json.load(open('src/Acme.TicketSystem.Domain.Shared/Localization/TicketSystem/zh-Hans.json',encoding='utf-8-sig'));print('ok')"
```

---

## Step 4 · 新建枚举 TicketLedgerEntryType

**新文件**：`src/Acme.TicketSystem.Domain/Tickets/TicketLedgerEntryType.cs`

```csharp
namespace Acme.TicketSystem.Tickets;

/// <summary>
/// 账务流水类型。数值刻意和原系统 tb_Account.PkNumber 保持一致，方便对照。
/// </summary>
public enum TicketLedgerEntryType : byte
{
    /// <summary>消费码（售票）</summary>
    Sale = 1,

    /// <summary>付款码（收款）</summary>
    Payment = 2,

    /// <summary>冲销消费（退票）</summary>
    RefundSale = 3,

    /// <summary>冲销付款（退票）</summary>
    RefundPayment = 4
}
```

**验证**：编译

---

## Step 5 · 新建实体 TicketLedgerEntry

**新文件**：`src/Acme.TicketSystem.Domain/Tickets/TicketLedgerEntry.cs`

```csharp
using System;
using Volo.Abp.Domain.Entities;

namespace Acme.TicketSystem.Tickets;

/// <summary>
/// 账务流水（聚合内实体）。对应原系统 tb_Account 的一条记录。
/// 金额字段带符号：正数是销售，负数是冲销。
/// </summary>
public class TicketLedgerEntry : Entity<Guid>
{
    public Guid TicketSaleId { get; private set; }

    public string TicketNo { get; private set; } = string.Empty;

    public TicketLedgerEntryType EntryType { get; private set; }

    public int Quantity { get; private set; }

    /// <summary>原系统 Charge。销售为正，冲销为负。</summary>
    public decimal Amount { get; private set; }

    /// <summary>原系统 Balance（应付款）。</summary>
    public decimal Balance { get; private set; }

    /// <summary>原系统 Credit（已付款）。</summary>
    public decimal Credit { get; private set; }

    public string? Reason { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private TicketLedgerEntry()
    {
        // EF Core
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
        DateTime createdAt)
        : base(id)
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
    /// 冲销：所有数值字段取反，类型换成对应的退款类型。
    /// 对应原系统 GetRefundCharge / GetRefundCredit 里那一串 * -1。
    /// </summary>
    internal TicketLedgerEntry Negate(DateTime createdAt)
    {
        var refundType = EntryType switch
        {
            TicketLedgerEntryType.Sale => TicketLedgerEntryType.RefundSale,
            TicketLedgerEntryType.Payment => TicketLedgerEntryType.RefundPayment,
            _ => throw new InvalidOperationException("只能冲销正向流水")
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
            createdAt);
    }
}
```

**验证**：编译，0 错误

```bash
dotnet build src/Acme.TicketSystem.Domain
```

---

## Step 6 · 改 TicketSale 聚合

**文件**：`src/Acme.TicketSystem.Domain/Tickets/TicketSale.cs`
**操作**：三处修改

### 6.1 加集合字段

找到 `private readonly List<SoldTicket> _tickets = new();`，在它下面加：

```csharp
    private readonly List<TicketLedgerEntry> _ledgerEntries = new();

    /// <summary>账务流水，对应原系统 tb_Account 的 4 种记录。</summary>
    public IReadOnlyCollection<TicketLedgerEntry> LedgerEntries => _ledgerEntries;
```

### 6.2 售票时生成两条正向流水

找到 `IssueTickets` 里的这段：

```csharp
            for (var i = 0; i < line.Quantity; i++)
            {
                _tickets.Add(new SoldTicket(
                    Guid.NewGuid(),
                    Id,
                    ticketNumbers[index++],
                    line.TicketCode,
                    line.TicketName,
                    line.UnitPrice));
            }
```

整个替换成：

```csharp
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

                // 1 = 消费码，对应原系统 GetChargeAccnt
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
                    createdAt: SoldAt));

                // 2 = 付款码，对应原系统 GetCreditAccnt
                _ledgerEntries.Add(new TicketLedgerEntry(
                    Guid.NewGuid(),
                    Id,
                    ticketNo,
                    TicketLedgerEntryType.Payment,
                    quantity: 1,
                    amount: 0,
                    balance: 0,
                    credit: line.UnitPrice,
                    reason: null,
                    createdAt: SoldAt));
            }
```

> 注意：我们的模型是"一张票一个票号"，所以 `quantity` 固定 1。
> 原系统是"一个票种一行一个票号 + 人数"，所以它写 `Quantity = input.Quantity`。
> 这是模型差异，不是抄错。

### 6.3 退票时生成两条冲销流水

找到 `RefundTicket` 整个方法，替换成：

```csharp
    public SoldTicket RefundTicket(string ticketNo, string? reason, DateTime refundedAt)
    {
        Check.NotNullOrWhiteSpace(ticketNo, nameof(ticketNo));

        var ticket = _tickets.FirstOrDefault(x => x.TicketNo == ticketNo)
                     ?? throw new BusinessException(TicketSystemDomainErrorCodes.TicketNotFound)
                         .WithData("TicketNo", ticketNo);

        // 找出这张票的两条正向流水：消费码 + 付款码
        var originals = _ledgerEntries
            .Where(x => x.TicketNo == ticketNo
                        && (x.EntryType == TicketLedgerEntryType.Sale
                            || x.EntryType == TicketLedgerEntryType.Payment))
            .ToList();

        if (originals.Count != 2)
        {
            throw new BusinessException(TicketSystemDomainErrorCodes.LedgerEntryNotFound)
                .WithData("TicketNo", ticketNo);
        }

        // 幂等保护：已经冲销过就不再生成第二条
        if (_ledgerEntries.Any(x => x.TicketNo == ticketNo
                                    && x.EntryType == TicketLedgerEntryType.RefundSale))
        {
            throw new BusinessException(TicketSystemDomainErrorCodes.LedgerAlreadyReversed)
                .WithData("TicketNo", ticketNo);
        }

        // 票标记已退（内部还会再拦一次重复退票）
        ticket.Refund(reason, refundedAt);

        // 冲销：原流水复制后所有数值取负
        foreach (var entry in originals)
        {
            _ledgerEntries.Add(entry.Negate(refundedAt));
        }

        return ticket;
    }
```

**验证**：编译 + 已有单测不挂

```bash
dotnet build src/Acme.TicketSystem.Domain
dotnet test test/Acme.TicketSystem.EntityFrameworkCore.Tests
```

---

## Step 7 · DTO 和映射

### 7.1 新增 DTO

**文件**：`src/Acme.TicketSystem.Application.Contracts/Tickets/TicketSaleDto.cs`
**操作**：文件末尾追加一个新类

```csharp
public class TicketLedgerEntryDto
{
    public string TicketNo { get; set; } = string.Empty;

    /// <summary>Sale / Payment / RefundSale / RefundPayment</summary>
    public string EntryType { get; set; } = string.Empty;

    public int Quantity { get; set; }
    public decimal Amount { get; set; }
    public decimal Balance { get; set; }
    public decimal Credit { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### 7.2 给 TicketSaleDto 加集合

同一个文件里的 `TicketSaleDto`，在 `public List<SoldTicketDto> Tickets { get; set; } = new();` 下面加：

```csharp
    public List<TicketLedgerEntryDto> LedgerEntries { get; set; } = new();
```

### 7.3 AutoMapper

**文件**：`src/Acme.TicketSystem.Application/TicketSystemApplicationAutoMapperProfile.cs`
**操作**：在构造函数里加两行

```csharp
        CreateMap<TicketLedgerEntry, TicketLedgerEntryDto>()
            .ForMember(d => d.EntryType, o => o.MapFrom(s => s.EntryType.ToString()));
```

**验证**：编译 Application

```bash
dotnet build src/Acme.TicketSystem.Application
```

---

## Step 8 · EF 映射

**文件**：`src/Acme.TicketSystem.EntityFrameworkCore/EntityFrameworkCore/TicketSystemDbContext.cs`
**操作**：两处

### 8.1 加 DbSet

在 `public DbSet<SoldTicket> SoldTickets { get; set; }` 下面加：

```csharp
    public DbSet<TicketLedgerEntry> TicketLedgerEntries { get; set; }
```

### 8.2 加实体配置

在 `builder.Entity<SoldTicket>(...)` 那段的后面加：

```csharp
        builder.Entity<TicketLedgerEntry>(b =>
        {
            b.ToTable(TicketSystemConsts.DbTablePrefix + "TicketLedgerEntries", TicketSystemConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.TicketNo).IsRequired().HasMaxLength(TicketConsts.MaxTicketNoLength);
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.Property(x => x.Balance).HasPrecision(18, 2);
            b.Property(x => x.Credit).HasPrecision(18, 2);
            b.Property(x => x.Reason).HasMaxLength(200);

            b.HasIndex(x => new { x.TicketSaleId, x.TicketNo });
        });
```

同一个文件的 `builder.Entity<TicketSale>(...)` 里，找到 `b.HasMany(x => x.Tickets)` 那段，在它后面加：

```csharp
            b.HasMany(x => x.LedgerEntries)
                .WithOne()
                .HasForeignKey(x => x.TicketSaleId)
                .OnDelete(DeleteBehavior.Cascade);
```

### 8.3 仓储必须 Include 流水集合

**文件**：`src/Acme.TicketSystem.EntityFrameworkCore/Tickets/EfCoreTicketSaleRepository.cs`

找到 `WithDetailsAsync()`，加一行 `Include`：

```csharp
    public override async Task<IQueryable<TicketSale>> WithDetailsAsync()
    {
        return (await GetQueryableAsync())
            .Include(x => x.Lines)
            .Include(x => x.Tickets)
            .Include(x => x.LedgerEntries);   // ← 新加的
    }
```

**为什么这一步不能漏**：EF 默认不加载导航集合。退票时 `FindByTicketNoAsync` 加载出来的聚合里 `LedgerEntries` 是空集合，`originals.Count` 是 0 不是 2，直接抛"找不到原始流水"。

症状很迷惑：**数据库里明明有流水，代码却说到处找不到**。

> 经验：聚合里每加一个集合导航，都要回来检查 `WithDetailsAsync` 有没有 Include。这是 DDD + EF 最容易踩的坑。

**验证**：编译 EF 项目

```bash
dotnet build src/Acme.TicketSystem.EntityFrameworkCore
```

---

## Step 9 · 生成迁移并建表

```bash
# 在解决方案根目录执行
dotnet ef migrations add AddTicketLedgerEntries \
  --project src/Acme.TicketSystem.EntityFrameworkCore \
  --startup-project src/Acme.TicketSystem.DbMigrator

# 建表 + 补权限
dotnet run --project src/Acme.TicketSystem.DbMigrator
```

**验证**：迁移文件里能看到 `AppTicketLedgerEntries`

```bash
grep -n "AppTicketLedgerEntries" src/Acme.TicketSystem.EntityFrameworkCore/Migrations/*AddTicketLedgerEntries.cs
```

---

## Step 10 · 页面验证（可选）

**文件**：`src/Acme.TicketSystem.Web/Pages/Tickets/Index.js`

在 `renderResult` 函数末尾追加一段渲染流水的代码，或者在结果卡片下面加一个表格。最小做法：控制台打印。

```javascript
console.table(result.ledgerEntries);
```

然后重启 Web，去 `/Tickets` 卖一张 120 元的票，控制台应该看到 2 行：

```
EntryType  Quantity  Amount  Balance  Credit
Sale       1         120     120      0
Payment    1         0       0        120
```

再点「退票」，应该变成 4 行，后两行全负。

---

## 抄完自检

| # | 检查 | 怎么看 |
|---|---|---|
| 1 | 售票生成 2 条流水 | 数据库 `AppTicketLedgerEntries` 有 2 行 |
| 2 | 退票变 4 条 | 退完再查，多 2 行 |
| 3 | 冲销是负数 | `Amount`、`Balance`、`Credit` 都是负 |
| 4 | 数量和金额一起取负 | `Quantity` 也是 -1 |
| 5 | 全生命周期合计为 0 | `SELECT SUM(Amount) FROM AppTicketLedgerEntries WHERE TicketNo = ...` 结果是 0 |
| 6 | 退两次被拦 | 第二次报"该票已经退过" |
| 7 | 单测不连库能跑 | `dotnet test` 绿 |

最后一条 SQL 是终极标准：

```sql
SELECT TicketNo, SUM(Amount) AS NetAmount, SUM(Quantity) AS NetQty
FROM AppTicketLedgerEntries
GROUP BY TicketNo;
```

每个票号的 `NetAmount` 和 `NetQty` 都应该是 **0**。

---

## 常见抄错的地方

| 现象 | 原因 |
|---|---|
| 流水只有 2 条 | 忘了在 `RefundTicket` 里加冲销循环 |
| 金额是 0 | `Negate()` 里漏了 `-Amount` |
| 数量变成正整数 | 只写了 `-Amount`，忘了 `-Quantity` |
| 编译报"只读集合不能 Add" | `LedgerEntries` 属性返回 `IReadOnlyCollection`，往 `_ledgerEntries` 里加才对 |
| 迁移里没有新表 | `DbSet` 加了但没配 `HasMany`，或者没重新生成迁移 |
| 页面看不到流水 | AutoMapper 没配 `EntryType` 的字符串映射，`LedgerEntries` 是空数组 |

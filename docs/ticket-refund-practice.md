# 退票账务冲销 —— 按原系统逻辑做 DDD 复刻

> 本文只复刻原系统**已经存在**的逻辑，不新增任何流程。
>
> 原码位置：`TicKet_Service/TicketService.cs`
> - `Refund(...)` 第 2244 行
> - `GetRefundCharge(...)` 第 2313 行
> - `GetRefundCredit(...)` 第 2349 行

---

## 0. 这份练习要解决的问题

当前项目里的退款只做了一件事：

```csharp
sale.RefundTicket(ticketNo, reason, Clock.Now);   // 票变成已退
```

**一条账务都没写。** 原系统退款时会往 `tb_Account` 插两条负数记录做冲销，这个逻辑在你的 DDD 版本里缺失了。

练习目标：把这两条冲销记录补上，并且用聚合的方式组织——不是照着 `_db.Insertable` 抄一遍。

---

## 1. 原系统做了什么

`Refund(ticketNO, CrtBy, refundId, ref msg, requestIp)` 的完整步骤：

| 步骤 | 原码 | 说明 |
|---|---|---|
| 1 | 查 `TicketNO` | 查不到 → "此票为废票，不能退" |
| 2 | `Sta == "F"` | → "此票已经退，不要重复退票" |
| 3 | `Sta == "R"` | → "此票为预订票，不能退票" |
| 4 | `BeginTran()` | 开事务 |
| 5 | 查 `Main`、`Account(PkNumber=1)`、`Account(PkNumber=2)` | 主单 + 消费码 + 付款码 |
| 6 | `GetRefundCharge(chargeAccount)` | 生成冲销消费（PkNumber=3） |
| 7 | `GetRefundCredit(creditAccount)` | 生成冲销付款（PkNumber=4） |
| 8 | `ticket.Sta = "F"`、`Modify`、`ModifyTime` | 票号表改状态 |
| 9 | `main.Sta = "F"`、`CancelTime` | 主单改状态 |
| 10 | `PcCode == "9010"` 时调拉卡拉退款 | 扫码支付的走三方退款 |
| 11 | 2 次 Update + 2 次 Insert + `CommitTran()` | 三张表在一个事务里 |

第 3 步的"预订票不能退"依赖 `Sta = "R"`；第 10 步的三方退款本次不做。

---

## 2. 账务记录长什么样

原表 `tb_Account` 用 `PkNumber` 区分四种记录：

| PkNumber | 含义 | 数量 | 金额 | 谁生成 |
|---|---|---|---|---|
| 1 | 消费码（售票） | 正 | 正（Charge、Balance） | 售票时 |
| 2 | 付款码（收款） | 正 | 正（Credit） | 售票时 |
| 3 | 冲销消费（退票） | **负** | **负** | 退票时 |
| 4 | 冲销付款（退票） | **负** | **负** | 退票时 |

冲销规则的原文（`GetRefundCharge`）：

```csharp
t.PkNumber = 3;
t.Quantity  = charge.Quantity  * -1;
t.Charge    = charge.Charge    * -1;
t.OthChg    = charge.OthChg    * -1;
t.ServerChg = charge.ServerChg * -1;
t.Balance   = charge.Balance   * -1;
t.Credit    = charge.Credit    * -1;
```

`GetRefundCredit` 除了 `PkNumber = 4`，其余一模一样——**所有数值字段取负**。

这就是本次练习的核心规则：**冲销 = 原记录的全部数值取反，其他字段复制。**

---

## 3. 当前项目差在哪

```
Domain/Tickets/TicketSale.cs
    IssueTickets()  → 只生成 SoldTicket，没有账务
    RefundTicket()  → 只把 SoldTicket.IsRefunded 置 true
```

| 原系统 | 当前项目 |
|---|---|
| 售票写 `tb_Main` + `tb_TicKetNO` + 2 条 `tb_Account` | 售票写 `SoldTicket` |
| 退票改 2 张表状态 + 插 2 条负数 `tb_Account` | 只改 `SoldTicket` 一个字段 |

缺的就是"钱"这一块。

---

## 4. 为什么必须放在 TicketSale 聚合里

原系统第 4 步 `BeginTran()`、第 11 步 `CommitTran()` 把**票状态 + 主单状态 + 四条账务**包在同一个事务里。

DDD 的规则是：**必须同时成功的一组变更，放在同一个聚合里。**

所以不是新建一个"退款单聚合"，而是在 `TicketSale` 里加一个账务集合：

```
TicketSale（聚合根）
├─ Lines         票种明细
├─ Tickets       每张票
└─ LedgerEntries 账务流水   ← 新增
```

这样 `RefundTicket()` 一次调用就同时改票状态和写冲销记录，一次 `SaveChanges` 全部落库——等价于原系统的 `BeginTran` / `CommitTran`。

> 顺带纠正上一版文档：那次我编了一个"退款单 + 审批"的独立聚合，那是新业务，不是复刻。

---

## 5. 聚合设计

### 5.1 新增实体 TicketLedgerEntry

| 字段 | 类型 | 对应原 `Account` |
|---|---|---|
| `TicketSaleId` | Guid | 归属票据 |
| `TicketNo` | string | 关联票号，便于按票查流水 |
| `EntryType` | TicketLedgerEntryType | `PkNumber` |
| `Quantity` | int | `Quantity`（有符号） |
| `Amount` | decimal | `Charge`（有符号） |
| `Balance` | decimal | `Balance`（有符号） |
| `Credit` | decimal | `Credit`（有符号） |
| `Reason` | string? | `Reason` |
| `CreatedAt` | DateTime | `CrtTime` |

原表的 `GID/StoreID/PcCode/TkType/TkStyle/Packages/Shift/Empno/MarketID/Channel` 在当前模型里没有对应概念，**可以不搬**——本次要复刻的是"取负冲销"这个规则，不是重建整张表。

### 5.2 枚举

```csharp
public enum TicketLedgerEntryType : byte
{
    Sale = 1,           // 原 PkNumber = 1
    Payment = 2,        // 原 PkNumber = 2
    RefundSale = 3,     // 原 PkNumber = 3
    RefundPayment = 4   // 原 PkNumber = 4
}
```

数值刻意和原表一致，方便对照。

### 5.3 TicketSale 要改的两个方法

| 方法 | 时机 | 生成 |
|---|---|---|
| `IssueTickets(numbers)` | 售票 | 每张票 2 条：`Sale`、`Payment` |
| `RefundTicket(ticketNo, reason, now)` | 退票 | 每张票 2 条：`RefundSale`、`RefundPayment` |

冲销记录怎么来（伪代码，自己实现）：

```
refundSale    = saleEntry.Negate()      // 数值全部 * -1，类型改成 RefundSale
refundPayment = paymentEntry.Negate()   // 同上，类型改成 RefundPayment
```

建议在 `TicketLedgerEntry` 上加一个 `Negate()` 方法，把"取负"收在一处——原系统两段代码重复了 20 行，你可以顺手消掉。

---

## 6. 不变量

| # | 不变量 | 来源 |
|---|---|---|
| 1 | 售票生成的 2 条流水金额为正 | `GetChargeAccnt` / `GetCreditAccnt` |
| 2 | 退款生成的 2 条流水，每个数值字段 = 对应正向流水的相反数 | `GetRefundCharge` / `GetRefundCredit` |
| 3 | 冲销必须能找到对应的正向流水，找不到就不能退 | 原码先查 `PkNumber=1`/`2` 再构造 |
| 4 | 同一张票只能冲销一次 | 原码 `Sta == "F"` 判断 |
| 5 | 票状态变更和冲销记录在同一事务 | `BeginTran` / `CommitTran` |
| 6 | 冲销的票号必须属于本聚合 | 聚合方法天然保证 |
| 7 | **数量也要取负**，不只是金额 | `Quantity * -1` |

第 7 条最容易漏。多数人只记得金额取负，忘了数量。

---

## 7. 三处必须你自己决策的差异

原系统有几个规则，当前项目的模型对不上，需要你选：

### 7.1 已检票能不能退

原码里这段是**注释掉的**：

```csharp
//if (ticket.Sta == "O")
//{
//    msg = "此票已检，不能退票。";
//    return false;
//}
```

说明当时线上是**允许已检票退款**的。

但你的项目里 `SoldTicket` 有 `IsValidated`。要不要拦，你自己定：

- 照原系统 → 不拦
- 按直觉 → 拦，抛一个业务异常

**建议照原系统**，因为这是复刻练习。

### 7.2 预订票（Sta = "R"）

原系统会拒绝 `Sta = "R"` 的票退款。你的 `SoldTicket` 没有"预订"状态，这条规则没有落脚点。

要么跳过，要么给 `SoldTicket` 加状态枚举——但那就是扩展模型，不是复刻了。

### 7.3 第三方退款（拉卡拉）

原码第 10 步，`PcCode == "9010"` 时调支付服务退款。

本次不做。如果要做，正确位置是**领域事件**（`TicketRefundedEvent` → 支付模块处理），不要在聚合里发 HTTP。

---

## 8. 文件清单

```
src/Acme.TicketSystem.Domain/
  Tickets/
    TicketLedgerEntryType.cs        ← 新增枚举
    TicketLedgerEntry.cs            ← 新增实体（含 Negate()）
    TicketSale.cs                   ← 加集合、改 IssueTickets / RefundTicket

src/Acme.TicketSystem.EntityFrameworkCore/
  EntityFrameworkCore/TicketSystemDbContext.cs   ← DbSet + 映射 + 索引
  Migrations/                                    ← 新迁移

src/Acme.TicketSystem.Application.Contracts/
  Tickets/TicketSaleDto.cs        ← 加 LedgerEntries 列表

src/Acme.TicketSystem.Web/Pages/Tickets/Index.js
                                    ← 结果表格下面显示流水（可选）
```

---

## 9. 分步实现

每步写完都要能编译、能验证，不要一口气写完再调。

### Step 1：枚举 + 实体

- `TicketLedgerEntryType`，四个值，数值 1~4
- `TicketLedgerEntry : Entity<Guid>`，字段见 5.1
- 加 `Negate()`：返回一条数值取反、类型换成对应退款类型的新记录
- **验证**：编译通过；临时测试 `Sale(2, 120, 120, 0).Negate()` 得到 `RefundSale(-2, -120, -120, 0)`

### Step 2：售票时生成正向流水

- `TicketSale` 加 `private readonly List<TicketLedgerEntry> _ledgerEntries`
- `IssueTickets` 里每张票生成 2 条：
  - `Sale`：Quantity = 票数，Amount = 单价，Balance = 单价，Credit = 0
  - `Payment`：Quantity = 票数，Amount = 0，Balance = 0，Credit = 单价

> 对应原码：`GetChargeAccnt` 里 `Balance = input.Charge`、`Credit = 0`；
> `GetCreditAccnt` 里 `Charge = 0`、`Credit = s.Balance`、`Balance = 0`。

- **验证**：卖一张票，`AppTicketLedgerEntries` 里应该有 2 行

### Step 3：退票时生成冲销流水

- `RefundTicket` 里，在 `ticket.Refund(...)` 之前：
  - 找到该票的两条正向流水（`Sale`、`Payment`）
  - 找不到 → 抛业务异常（不变量 3）
  - 各调一次 `Negate()` 加进集合
- **验证**：退一张票，流水从 2 条变 4 条，后两条全是负数

### Step 4：纯内存单测（重点）

**不连数据库**：

| 用例 | 期望 |
|---|---|
| 售票后 | 2 条流水，都为正 |
| 退票后 | 4 条，第 3/4 条 = 第 1/2 条取反 |
| 退两次 | 第二次抛异常 |
| 手动删掉正向流水再退 | 抛"找不到账务" |
| 数量 3、单价 120 | 冲销是 -3 和 -360 |

### Step 5：EF 映射 + 迁移

- `DbSet<TicketLedgerEntry>`，表名 `AppTicketLedgerEntries`
- `Amount / Balance / Credit` 用 `decimal(18,2)`
- 索引：`(TicketSaleId, TicketNo)`
- 生成迁移并跑 DbMigrator
- **验证**：表建出来，字段类型对

### Step 6：DTO + 页面展示

- `TicketSaleDto` 加 `LedgerEntries`
- 售票页结果卡片下面加流水表格：类型、数量、金额、余额、时间
- **验证**：卖一张、退一张，页面上能看到 4 行

---

## 10. 验收用例

| # | 操作 | 期望 |
|---|---|---|
| 1 | 卖 1 张 120 元的票 | 流水 2 条：Sale(+1, +120, Balance 120)、Payment(+1, Credit 120) |
| 2 | 退这张票 | 流水 4 条，新增 RefundSale(-1, -120, Balance -120)、RefundPayment(-1, Credit -120) |
| 3 | 再退一次 | 抛"该票已经退过" |
| 4 | 卖 3 张 | 冲销的 Quantity 是 -3，不是 -1 |
| 5 | 已核销的票退款 | 按 7.1 的决定：允许 或 拒绝 |
| 6 | 查数据库 | 4 条记录在同一张单下，金额正负相抵为 0 |

第 6 条是判断"冲销正确"的终极标准：**一张票的全生命周期流水，金额合计必须为 0。**

---

## 11. 常见错误

### 错误 1：只把金额取负，忘了数量

```csharp
refund.Quantity = original.Quantity;      // 原系统是 * -1
```

结果：金额对上了，数量对不上，对账报表里人数是双倍。

### 错误 2：冲销记录和票状态分两次保存

```csharp
await _repo.UpdateAsync(sale);            // 第一次
await _ledgerRepo.InsertAsync(entry);     // 第二次，中间失败就不一致
```

正确：冲销记录是聚合内的集合，一次 `SaveChanges` 全写完。

### 错误 3：用新聚合装冲销记录

```
TicketRefund（新聚合） → 保存 → 再改 TicketSale
```

除非你承认"票退了但账没冲"是可以接受的中间状态，否则必须同聚合。

### 错误 4：冲销时重新计算金额

```csharp
refund.Amount = ticket.Amount * -1;   // 应该取原流水的值
```

如果当初有折扣、优惠，票面金额和实际流水金额不一样，重新算就错了。**必须复制原流水再取负。**

### 错误 5：忘记校验正向流水存在

直接取负会空引用，或者生成一条没有对应正项的孤儿冲销。

---

## 12. 参考

| 参考什么 | 看哪个文件 |
|---|---|
| 聚合怎么写、怎么抛异常 | `Domain/Tickets/TicketSale.cs` |
| 聚合内实体怎么定义 | `Domain/Tickets/TicketSaleLine.cs`、`SoldTicket.cs` |
| 事务边界怎么保证 | `Application/Tickets/TicketAppService.cs` |
| EF 集合映射（HasMany） | `EntityFrameworkCore/EntityFrameworkCore/TicketSystemDbContext.cs` |
| **原系统逻辑** | `TicketService.Refund` L2244、`GetRefundCharge` L2313、`GetRefundCredit` L2349 |

---

## 13. 完成后自检

- [ ] `Negate()` 把**数量、金额、余额、信用**四个数值都取了负
- [ ] 售票生成 2 条，退票生成 2 条，合计 4 条
- [ ] 一张票的全生命周期流水，金额合计为 0
- [ ] 冲销记录是从原流水复制后取负，不是重新计算
- [ ] 退票后票状态和冲销记录在同一次 `SaveChanges` 里
- [ ] 找不到正向流水时会抛异常，不是生成孤儿记录
- [ ] 单测不连数据库也能跑

---

## 附：一句话

> 原系统：`BeginTran → Update 票 → Update 主单 → Insert 负数消费 → Insert 负数付款 → Commit`
>
> DDD：`聚合方法里同时改票状态和加流水 → 一次 SaveChanges`
>
> 换的是组织方式，**规则一条都不能变**。

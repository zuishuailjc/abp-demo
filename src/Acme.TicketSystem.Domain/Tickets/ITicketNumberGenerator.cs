using System.Collections.Generic;
using System.Threading.Tasks;

namespace Acme.TicketSystem.Tickets;

/// <summary>
/// 票号生成器（领域服务）。
/// 原项目是数据库函数 FUNC_TicKetNO('T', GID, StoreID)，这里抽象成接口，
/// 让聚合不依赖数据库，实现放在基础设施层。
/// </summary>
public interface ITicketNumberGenerator
{
    /// <summary>
    /// 生成 count 个连续票号，例如 T202610300001、T202610300002。
    /// </summary>
    Task<IReadOnlyList<string>> GenerateAsync(string prefix, int count);
}

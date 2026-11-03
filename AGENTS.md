# CODEBUDDY.md This file provides guidance to CodeBuddy when working with code in this repository.

本文件面向在本仓库中工作的 AI 助手，用中文描述常用命令与整体架构。

## 项目概览

`Acme.TicketSystem` 是基于 **ABP Framework 8.3.0**（.NET 8）的 DDD 分层单体应用，UI 为 ASP.NET Core MVC / Razor Pages + LeptonX Lite 主题，数据库为 **SQL Server**（库名 `abpTicket`），认证由 **OpenIddict** 提供。当前仓库基本处于 ABP 启动模板状态：尚无自定义实体，只有示例/基础设施代码。

## 常用命令

所有命令均在解决方案根目录执行。

```bash
# 构建整个解决方案
dotnet build Acme.TicketSystem.sln

# 启动 Web 应用（https://localhost:44347）
dotnet run --project src/Acme.TicketSystem.Web

# 热重载开发
dotnet watch run --project src/Acme.TicketSystem.Web

# 安装客户端库（在 src/Acme.TicketSystem.Web 目录执行；一般由 ABP CLI 自动完成）
abp install-libs

# 应用迁移并填充种子数据（首次运行/新增迁移后必须执行）
# 注意：必须在 src/Acme.TicketSystem.DbMigrator 目录下执行，
#       DbMigrator 用相对路径读自己的 appsettings.json，工作目录不对会报
#       "The ConnectionString property has not been initialized."
cd src/Acme.TicketSystem.DbMigrator && dotnet run

# 新增 EF Core 迁移
# 同理：必须在 src/Acme.TicketSystem.EntityFrameworkCore 目录下执行，
#       TicketSystemDbContextFactory 用 ../Acme.TicketSystem.DbMigrator/appsettings.json 找连接串
cd src/Acme.TicketSystem.EntityFrameworkCore
dotnet ef migrations add <MigrationName> --project . --startup-project ../Acme.TicketSystem.DbMigrator

# 运行全部测试
dotnet test Acme.TicketSystem.sln

# 只运行某一类测试（示例：应用服务测试）
dotnet test test/Acme.TicketSystem.EntityFrameworkCore.Tests --filter "FullyQualifiedName~SampleAppServiceTests"

# 运行单个测试方法
dotnet test test/Acme.TicketSystem.Web.Tests --filter "FullyQualifiedName~Index_Tests.Welcome_Page"
```

说明：`Domain.Tests` 与 `Application.Tests` 中的测试类都是泛型抽象基类，真正的测试在 `EntityFrameworkCore.Tests`（SQLite 内存库）和 `Web.Tests` 中派生执行。

## 架构总览

### 分层与依赖方向

```
Domain.Shared  ←  Domain  ←  Application.Contracts  ←  Application  ←  HttpApi / HttpApi.Client
      ↑              ↑                                        ↑
      └──────────────┴──── EntityFrameworkCore  ←────────────┴──── Web / DbMigrator / *.Tests
```

- `src/Acme.TicketSystem.Domain.Shared`：常量、错误码、本地化资源 `TicketSystemResource`、`MultiTenancyConsts`、全局 Feature 与模块扩展配置。最底层，不引用其他业务项目。
- `src/Acme.TicketSystem.Domain`：领域层，引用所有基础模块的 `*.Domain` 包；`OpenIddictDataSeedContributor` 负责 OpenIddict 客户端/作用域的种子数据，`Data/TicketSystemDbMigrationService` 是 DbMigrator 调用的迁移入口。
- `src/Acme.TicketSystem.Application.Contracts`：DTO、应用服务接口、权限定义（`TicketSystemPermissions` / `TicketSystemPermissionDefinitionProvider`）、`TicketSystemDtoExtensions`。
- `src/Acme.TicketSystem.Application`：应用服务实现（当前仅 `TicketSystemAppService` 基类）与 AutoMapper 配置。
- `src/Acme.TicketSystem.HttpApi`：`TicketSystemController` 基类及 HTTP 模型。Web 项目通过 ABP **约定式控制器**（`ConventionalControllers.Create(typeof(TicketSystemApplicationModule).Assembly)`）自动把应用服务暴露为 API，通常无需手写 Controller。
- `src/Acme.TicketSystem.HttpApi.Client`：供远程客户端（见 `test/.../HttpApi.Client.ConsoleTestApp`）使用的动态代理。
- `src/Acme.TicketSystem.EntityFrameworkCore`：唯一的 DbContext `TicketSystemDbContext`，同时实现 `IIdentityDbContext`、`ITenantManagementDbContext` 并用 `[ReplaceDbContext]` 替换模块自带 DbContext，从而可跨模块 JOIN。`Migrations/` 存放迁移。
- `src/Acme.TicketSystem.Web`：宿主。`Program.cs` → `AddApplicationAsync<TicketSystemWebModule>()` → `InitializeApplicationAsync()`；`TicketSystemWebModule.DependsOn` 聚合 HTTP API、EF Core、Autofac、Identity/Account/Tenant/Setting 等模块，并在 `OnApplicationInitialization` 中编排中间件顺序（本地化 → 认证 → OpenIddict 校验 → 多租户 → UoW → 动态声明 → 授权 → Swagger → 审计 → 端点）。
- `src/Acme.TicketSystem.DbMigrator`：承载 `DbMigratorHostedService` 的控制台宿主，独立读取自己的 `appsettings.json`（含 OpenIddict 客户端定义），执行迁移和 `IDataSeeder`。

### 关键约定

- **配置**：连接串在 `Web/appsettings.json` 与 `DbMigrator/appsettings.json` 中各写一份，修改数据库时两处都要改；敏感配置可放 `appsettings.secrets.json` 或 User Secrets（`UserSecretsId` 已在 Web 的 csproj 中声明）。
- **迁移工厂依赖工作目录**：`TicketSystemDbContextFactory` 用相对路径 `../Acme.TicketSystem.DbMigrator/appsettings.json` 读取连接串，因此执行 `dotnet ef` 时工作目录必须能解析到该路径；迁移工具来自 EF 项目内的 `Microsoft.EntityFrameworkCore.Tools`。
- **多租户**：由 `MultiTenancyConsts.IsEnabled`（当前 `true`）在 `TicketSystemDomainModule` 统一开关，`Web` 模块据此决定是否 `UseMultiTenancy()`。新增实体时需考虑 `IMultiTenant`。
- **权限/菜单/本地化扩展点**：权限在 `Application.Contracts/Permissions`，菜单在 `Web/Menus`（`TicketSystemMenus` + `TicketSystemMenuContributor` 注册到 `AbpNavigationOptions`），本地化资源在 `Domain.Shared/Localization` 并以虚拟文件系统嵌入。
- **虚拟文件系统**：Development 环境下 `ConfigureVirtualFileSystem` 把各模块的嵌入式 Razor/JSON 资源重定向到源码目录（`../Acme.TicketSystem.*`），因此可以热编辑 `.cshtml`/本地化文件；这些相对路径与目录结构绑定，移动项目目录时需同步修改。
- **OpenIddict**：非 Development 环境使用 `openiddict.pfx`（密码见 `TicketSystemWebModule`）作为签名/加密证书；Development 使用开发用证书。客户端与作用域种子数据在 `Domain/OpenIddict/OpenIddictDataSeedContributor.cs`。
- **新增实体流程**：在 `Domain` 定义实体 → `TicketSystemDbContext` 添加 `DbSet` 并在 `OnModelCreating` 中 `builder.Entity<T>(...)`（用 `TicketSystemConsts.DbTablePrefix` + `ConfigureByConvention()`）→ 生成迁移 → 运行 DbMigrator。若实体需要按模块扩展属性，在 `Domain.Shared/TicketSystemModuleExtensionConfigurator` 定义、在 EF 项目的 `TicketSystemEfCoreEntityExtensionMappings` 映射。
- **日志**：Serilog 写入 `Logs/logs.txt` 与控制台，Web 与 DbMigrator 均通过 `Program.cs` 中的 `LoggerConfiguration` 初始化。

### 测试架构

- `test/Acme.TicketSystem.TestBase`：公共测试基类与模块，关闭后台作业执行、授权始终放行（`AddAlwaysAllowAuthorization`），并在应用初始化时调用 `IDataSeeder.SeedAsync()` 注入 admin 用户/租户等初始数据。
- `Domain.Tests` / `Application.Tests`：提供 `TicketSystemDomainTestBase<TStartupModule>` 等泛型抽象基类和 `Sample*Tests` 抽象用例，本身不直接运行。
- `EntityFrameworkCore.Tests`：`TicketSystemEntityFrameworkCoreTestModule` 用 **SQLite 内存连接**（`Data Source=:memory:`，由 `CreateTables()` 建表）替换 SQL Server，并关闭静态 Permission/Setting/Feature 落库与 UoW 事务；具体测试在 `EntityFrameworkCore/{Applications,Domains,Samples}` 中派生基类运行。
- `Web.Tests`：`TicketSystemWebTestModule` 组合 Web + 测试模块，清空 OpenIddict 服务端预配置、启用开发证书，用于端到端页面/HTTP 断言。
- 断言库为 **Shouldly**，测试框架为 **xUnit**。

### 其他

- 目标框架 `net8.0`，`Nullable` 全局启用，公共属性（版本、`LangVersion`、`NoWarn`）在根目录 `common.props` 中统一维护，新增项目需 `<Import Project="..\..\common.props" />`。
- `NuGet.Config` 显式清空了 `packageSources`，实际使用的是本机全局 NuGet 源，还原失败时先检查全局配置。
- 前端资源通过 `abp install-libs` 还原到 `wwwroot/libs`（被 `.gitignore` 忽略，勿手动编辑）。
- 生产部署前需准备 `openiddict.pfx` 并修改连接串与证书密码。

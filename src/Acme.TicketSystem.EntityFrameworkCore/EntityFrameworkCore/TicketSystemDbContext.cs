using System;
using Acme.TicketSystem.Tickets;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.TenantManagement;
using Volo.Abp.TenantManagement.EntityFrameworkCore;

namespace Acme.TicketSystem.EntityFrameworkCore;

[ReplaceDbContext(typeof(IIdentityDbContext))]
[ReplaceDbContext(typeof(ITenantManagementDbContext))]
[ConnectionStringName("Default")]
public class TicketSystemDbContext :
	AbpDbContext<TicketSystemDbContext>,
	IIdentityDbContext,
	ITenantManagementDbContext
{
	/* Add DbSet properties for your Aggregate Roots / Entities here. */

	#region Entities of this application

	public DbSet<TicketSale> TicketSales { get; set; }

	public DbSet<TicketSaleLine> TicketSaleLines { get; set; }

	public DbSet<SoldTicket> SoldTickets { get; set; }
	public DbSet<TicketLedgerEntry> TicketLedgerEntries { get; set; }

	// ---- 票种目录：独立的聚合，定价时读它 ----
	public DbSet<TicketPrice> TicketPrices { get; set; }

	#endregion

	#region Entities from the modules

	/* Notice: We only implemented IIdentityDbContext and ITenantManagementDbContext
     * and replaced them for this DbContext. This allows you to perform JOIN
     * queries for the entities of these modules over the repositories easily. You
     * typically don't need that for other modules. But, if you need, you can
     * implement the DbContext interface of the needed module and use ReplaceDbContext
     * attribute just like IIdentityDbContext and ITenantManagementDbContext.
     *
     * More info: Replacing a DbContext of a module ensures that the related module
     * uses this DbContext on runtime. Otherwise, it will use its own DbContext class.
     */

	//Identity
	public DbSet<IdentityUser> Users { get; set; }
	public DbSet<IdentityRole> Roles { get; set; }
	public DbSet<IdentityClaimType> ClaimTypes { get; set; }
	public DbSet<OrganizationUnit> OrganizationUnits { get; set; }
	public DbSet<IdentitySecurityLog> SecurityLogs { get; set; }
	public DbSet<IdentityLinkUser> LinkUsers { get; set; }
	public DbSet<IdentityUserDelegation> UserDelegations { get; set; }
	public DbSet<IdentitySession> Sessions { get; set; }
	// Tenant Management
	public DbSet<Tenant> Tenants { get; set; }
	public DbSet<TenantConnectionString> TenantConnectionStrings { get; set; }

	#endregion

	public TicketSystemDbContext(DbContextOptions<TicketSystemDbContext> options)
		: base(options)
	{

	}

	protected override void OnModelCreating(ModelBuilder builder)
	{
		base.OnModelCreating(builder);

		/* Include modules to your migration db context */

		builder.ConfigurePermissionManagement();
		builder.ConfigureSettingManagement();
		builder.ConfigureBackgroundJobs();
		builder.ConfigureAuditLogging();
		builder.ConfigureIdentity();
		builder.ConfigureOpenIddict();
		builder.ConfigureFeatureManagement();
		builder.ConfigureTenantManagement();

		/* Configure your own tables/entities inside here */

		// ================= 售票聚合 =================
		// 一个聚合根 + 两个子实体集合，三张表通过 TicketSaleId 关联。
		// 应用层一次 InsertAsync 就能把三张表写完，事务由 ABP 的 UoW 兜住。
		builder.Entity<TicketSale>(b =>
		{
			b.ToTable(TicketSystemConsts.DbTablePrefix + "TicketSales", TicketSystemConsts.DbSchema);
			b.ConfigureByConvention();

			b.Property(x => x.SaleNo).IsRequired().HasMaxLength(TicketConsts.MaxSaleNoLength);
			b.Property(x => x.StoreId).IsRequired().HasMaxLength(TicketConsts.MaxStoreIdLength);
			b.Property(x => x.SoldBy).IsRequired().HasMaxLength(TicketConsts.MaxSoldByLength);
			b.Property(x => x.CustomerName).HasMaxLength(TicketConsts.MaxCustomerNameLength);
			b.Property(x => x.CustomerPhone).HasMaxLength(TicketConsts.MaxCustomerPhoneLength);

			// 计算属性不落库
			b.Ignore(x => x.TotalAmount);
			b.Ignore(x => x.TotalQuantity);

			b.HasIndex(x => x.SaleNo).IsUnique();

			b.HasMany(x => x.Lines)
				.WithOne()
				.HasForeignKey(x => x.TicketSaleId)
				.OnDelete(DeleteBehavior.Cascade);

			b.HasMany(x => x.Tickets)
				.WithOne()
				.HasForeignKey(x => x.TicketSaleId)
				.OnDelete(DeleteBehavior.Cascade);

			b.HasMany(x => x.LedgerEntries).WithOne().HasForeignKey(x => x.TicketSaleId).OnDelete(DeleteBehavior.Cascade);
		});

		builder.Entity<TicketSaleLine>(b =>
		{
			b.ToTable(TicketSystemConsts.DbTablePrefix + "TicketSaleLines", TicketSystemConsts.DbSchema);
			b.ConfigureByConvention();

			b.Property(x => x.TicketCode).IsRequired().HasMaxLength(TicketConsts.MaxTicketCodeLength);
			b.Property(x => x.TicketName).IsRequired().HasMaxLength(TicketConsts.MaxTicketNameLength);
			b.Property(x => x.UnitPrice).HasPrecision(18, 2);

			b.Ignore(x => x.Amount);
			b.HasIndex(x => x.TicketSaleId);
		});

		builder.Entity<SoldTicket>(b =>
		{
			b.ToTable(TicketSystemConsts.DbTablePrefix + "SoldTickets", TicketSystemConsts.DbSchema);
			b.ConfigureByConvention();

			b.Property(x => x.TicketNo).IsRequired().HasMaxLength(TicketConsts.MaxTicketNoLength);
			b.Property(x => x.TicketCode).IsRequired().HasMaxLength(TicketConsts.MaxTicketCodeLength);
			b.Property(x => x.TicketName).IsRequired().HasMaxLength(TicketConsts.MaxTicketNameLength);
			b.Property(x => x.Amount).HasPrecision(18, 2);

			// 票号全局唯一，这是并发取号出错时的最后一道防线
			b.HasIndex(x => x.TicketNo).IsUnique();
			b.HasIndex(x => x.TicketSaleId);
		});

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

		// ================= 票种目录 =================
		builder.Entity<TicketPrice>(b =>
		{
			b.ToTable(TicketSystemConsts.DbTablePrefix + "TicketPrices", TicketSystemConsts.DbSchema);
			b.ConfigureByConvention();

			b.Property(x => x.TicketCode).IsRequired().HasMaxLength(TicketConsts.MaxTicketCodeLength);
			b.Property(x => x.TicketName).IsRequired().HasMaxLength(TicketConsts.MaxTicketNameLength);
			b.Property(x => x.UnitPrice).HasPrecision(18, 2);

			// 票种编码唯一：目录里不能出现两个同编码的票种，否则定价结果不确定
			b.HasIndex(x => x.TicketCode).IsUnique();
		});
	}
}

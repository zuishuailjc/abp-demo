using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Acme.TicketSystem.Data;
using Volo.Abp.DependencyInjection;

namespace Acme.TicketSystem.EntityFrameworkCore;

public class EntityFrameworkCoreTicketSystemDbSchemaMigrator
    : ITicketSystemDbSchemaMigrator, ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public EntityFrameworkCoreTicketSystemDbSchemaMigrator(
        IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync()
    {
        /* We intentionally resolve the TicketSystemDbContext
         * from IServiceProvider (instead of directly injecting it)
         * to properly get the connection string of the current tenant in the
         * current scope.
         */

        await _serviceProvider
            .GetRequiredService<TicketSystemDbContext>()
            .Database
            .MigrateAsync();
    }
}

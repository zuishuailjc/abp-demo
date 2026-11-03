using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace Acme.TicketSystem.Data;

/* This is used if database provider does't define
 * ITicketSystemDbSchemaMigrator implementation.
 */
public class NullTicketSystemDbSchemaMigrator : ITicketSystemDbSchemaMigrator, ITransientDependency
{
    public Task MigrateAsync()
    {
        return Task.CompletedTask;
    }
}

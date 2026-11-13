using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FlowOps.Api;

internal sealed class FlowOpsDesignTime : IDesignTimeDbContextFactory<FlowOpsDb>
{
    public FlowOpsDb CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<FlowOpsDb>()
        .UseNpgsql("Host=localhost;Database=flowops", postgres =>
            postgres.MigrationsHistoryTable("__EFMigrationsHistory", "flowops")).Options);
}

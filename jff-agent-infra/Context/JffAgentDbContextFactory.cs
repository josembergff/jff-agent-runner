using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infra.Context;

// Used only by EF Core design-time tools (dotnet ef migrations ...)
public class JffAgentDbContextFactory : IDesignTimeDbContextFactory<JffAgentDbContext>
{
    public JffAgentDbContext CreateDbContext(string[] args)
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "jff-agent-runner");
        Console.Clear();
        Console.WriteLine($"Data directory factory: {dataDirectory}");
        Directory.CreateDirectory(dataDirectory);

        var options = new DbContextOptionsBuilder<JffAgentDbContext>()
            .UseSqlite($"Data Source={Path.Combine(dataDirectory, "database.db")}")
            .Options;

        return new JffAgentDbContext(options);
    }
}

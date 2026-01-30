using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DocumentIA.Data.Context;

public class DocumentIADbContextFactory : IDesignTimeDbContextFactory<DocumentIADbContext>
{
    public DocumentIADbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DocumentIADbContext>();
        
        // Connection string para desarrollo local
        var connectionString = "Server=localhost,1433;Database=DocumentIA;User Id=sa;Password=COMPLETAR_SQL_PASSWORD;TrustServerCertificate=True;";
        
        optionsBuilder.UseSqlServer(connectionString);

        return new DocumentIADbContext(optionsBuilder.Options);
    }
}

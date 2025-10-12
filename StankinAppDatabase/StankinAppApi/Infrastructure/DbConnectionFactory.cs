using Npgsql;

namespace StankinAppApi.Infrastructure;

public interface IDbConnectionFactory
{
    NpgsqlConnection CreateConnection();
}

public class PostgreSqlDbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public PostgreSqlDbConnectionFactory(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Missing 'Postgres' connection string.");
    }

    public NpgsqlConnection CreateConnection() => new(_connectionString);
}
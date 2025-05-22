using Serilog;
using Serilog.Sinks.PostgreSQL;
using Serilog.Sinks.PostgreSQL.ColumnWriters;
using NpgsqlTypes;

namespace GeoProfiles.Infrastructure.Extensions;

public static class SerilogExtensions
{
    private const string Table = "system_logs";
    private const string SchemaName = "public";

    public static WebApplicationBuilder AddSerilogLogging(this WebApplicationBuilder builder)
    {
        var connStr = builder.Configuration.GetConnectionString("DefaultConnection");

        var columnOptions = new Dictionary<string, ColumnWriterBase>
        {
            {"request_id", new SinglePropertyColumnWriter("RequestId", PropertyWriteMethod.Raw)},
            {"raise_date", new TimestampColumnWriter()},
            {"message", new RenderedMessageColumnWriter()},
            {"level", new LevelColumnWriter(true, NpgsqlDbType.Varchar)},
            {"exception", new ExceptionColumnWriter()},
        };

        if (connStr != null)
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.PostgreSQL(
                    connectionString: connStr,
                    tableName: Table,
                    schemaName: SchemaName,
                    needAutoCreateTable: true,
                    columnOptions: columnOptions,
                    period: TimeSpan.FromSeconds(5)
                )
                .CreateLogger();

        builder.Host.UseSerilog();
        return builder;
    }
}
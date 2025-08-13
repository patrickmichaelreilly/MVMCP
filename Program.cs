using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Server;
using System.ComponentModel;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

var app = builder.Build();

MicrovellumTools.Initialize(app.Services.GetRequiredService<IConfiguration>());

await app.RunAsync();

[McpServerToolType]
public static class MicrovellumTools
{
    private static IConfiguration? _configuration;
    
    public static void Initialize(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    [McpServerTool, Description("A simple hello world tool to test connectivity")]
    public static string Hello(string? name = null) 
        => $"Hello, {name ?? "World"}! This is the Microvellum MCP Server responding.";

    [McpServerTool, Description("Get the default connection string from configuration")]
    public static string GetDefaultConnectionString()
    {
        if (_configuration == null)
        {
            return JsonSerializer.Serialize(new { error = "Configuration not initialized" });
        }
        
        var connectionString = _configuration.GetConnectionString("Production");
        if (string.IsNullOrEmpty(connectionString))
        {
            return JsonSerializer.Serialize(new { error = "No Production connection string found in configuration" });
        }
        
        return connectionString;
    }

    [McpServerTool, Description("Test connection to SQL Server")]
    public static async Task<string> TestSqlConnection(string connectionString)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            var serverVersion = connection.ServerVersion;
            var database = connection.Database;
            
            return $"Successfully connected to SQL Server!\nServer Version: {serverVersion}\nDatabase: {database}";
        }
        catch (Exception ex)
        {
            return $"Connection failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get connection string for Microvellum database")]
    public static string GetConnectionString(string server, string database, string userId, string password)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = database,
            UserID = userId,
            Password = password,
            TrustServerCertificate = true,
            MultipleActiveResultSets = true
        };
        
        return builder.ConnectionString;
    }

    [McpServerTool, Description("Execute SELECT-only SQL queries safely. Only SELECT statements are allowed for data safety.")]
    public static async Task<string> Query(string connectionString, string sql)
    {
        if (!IsSelectQuery(sql))
        {
            return JsonSerializer.Serialize(new 
            { 
                error = "Only SELECT statements are allowed for safety. This query appears to modify data.",
                queryType = "BLOCKED"
            });
        }

        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            using var command = new SqlCommand(sql, connection);
            using var adapter = new SqlDataAdapter(command);
            var dataTable = new DataTable();
            adapter.Fill(dataTable);
            
            var results = new List<Dictionary<string, object?>>();
            foreach (DataRow row in dataTable.Rows)
            {
                var dict = new Dictionary<string, object?>();
                foreach (DataColumn col in dataTable.Columns)
                {
                    dict[col.ColumnName] = row[col] == DBNull.Value ? null : row[col];
                }
                results.Add(dict);
            }
            
            return JsonSerializer.Serialize(new 
            { 
                rowCount = results.Count,
                columns = dataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray(),
                data = results
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new 
            { 
                error = ex.Message,
                queryType = "ERROR"
            });
        }
    }

    [McpServerTool, Description("Get database schema information including tables, columns, relationships, and constraints")]
    public static async Task<string> GetSchema(string connectionString, string? tableName = null)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            var schema = new Dictionary<string, object>();
            
            if (string.IsNullOrEmpty(tableName))
            {
                var tablesQuery = @"
                    SELECT 
                        t.TABLE_SCHEMA,
                        t.TABLE_NAME,
                        t.TABLE_TYPE
                    FROM INFORMATION_SCHEMA.TABLES t
                    WHERE t.TABLE_TYPE IN ('BASE TABLE', 'VIEW')
                    ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME";
                
                using var tablesCmd = new SqlCommand(tablesQuery, connection);
                using var tablesAdapter = new SqlDataAdapter(tablesCmd);
                var tablesData = new DataTable();
                tablesAdapter.Fill(tablesData);
                
                var tables = new List<object>();
                foreach (DataRow row in tablesData.Rows)
                {
                    tables.Add(new
                    {
                        schema = row["TABLE_SCHEMA"].ToString(),
                        name = row["TABLE_NAME"].ToString(),
                        type = row["TABLE_TYPE"].ToString()
                    });
                }
                schema["tables"] = tables;
                schema["tableCount"] = tables.Count;
            }
            else
            {
                var columnsQuery = @"
                    SELECT 
                        c.COLUMN_NAME,
                        c.DATA_TYPE,
                        c.CHARACTER_MAXIMUM_LENGTH,
                        c.NUMERIC_PRECISION,
                        c.NUMERIC_SCALE,
                        c.IS_NULLABLE,
                        c.COLUMN_DEFAULT
                    FROM INFORMATION_SCHEMA.COLUMNS c
                    WHERE c.TABLE_NAME = @tableName
                    ORDER BY c.ORDINAL_POSITION";
                
                using var columnsCmd = new SqlCommand(columnsQuery, connection);
                columnsCmd.Parameters.AddWithValue("@tableName", tableName);
                using var columnsAdapter = new SqlDataAdapter(columnsCmd);
                var columnsData = new DataTable();
                columnsAdapter.Fill(columnsData);
                
                var columns = new List<object>();
                foreach (DataRow row in columnsData.Rows)
                {
                    columns.Add(new
                    {
                        name = row["COLUMN_NAME"].ToString(),
                        dataType = row["DATA_TYPE"].ToString(),
                        maxLength = row["CHARACTER_MAXIMUM_LENGTH"] == DBNull.Value ? null : row["CHARACTER_MAXIMUM_LENGTH"],
                        precision = row["NUMERIC_PRECISION"] == DBNull.Value ? null : row["NUMERIC_PRECISION"],
                        scale = row["NUMERIC_SCALE"] == DBNull.Value ? null : row["NUMERIC_SCALE"],
                        nullable = row["IS_NULLABLE"].ToString() == "YES",
                        defaultValue = row["COLUMN_DEFAULT"] == DBNull.Value ? null : row["COLUMN_DEFAULT"].ToString()
                    });
                }
                
                var keysQuery = @"
                    SELECT 
                        kcu.COLUMN_NAME,
                        tc.CONSTRAINT_TYPE,
                        tc.CONSTRAINT_NAME
                    FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                    JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc 
                        ON kcu.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
                    WHERE kcu.TABLE_NAME = @tableName
                    ORDER BY tc.CONSTRAINT_TYPE, kcu.ORDINAL_POSITION";
                
                using var keysCmd = new SqlCommand(keysQuery, connection);
                keysCmd.Parameters.AddWithValue("@tableName", tableName);
                using var keysAdapter = new SqlDataAdapter(keysCmd);
                var keysData = new DataTable();
                keysAdapter.Fill(keysData);
                
                var constraints = new List<object>();
                foreach (DataRow row in keysData.Rows)
                {
                    constraints.Add(new
                    {
                        column = row["COLUMN_NAME"].ToString(),
                        type = row["CONSTRAINT_TYPE"].ToString(),
                        name = row["CONSTRAINT_NAME"].ToString()
                    });
                }
                
                var fkQuery = @"
                    SELECT 
                        fk.name AS FK_NAME,
                        cp.name AS PARENT_COLUMN,
                        rt.name AS REFERENCED_TABLE,
                        cr.name AS REFERENCED_COLUMN
                    FROM sys.foreign_keys fk
                    INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                    INNER JOIN sys.tables t ON fkc.parent_object_id = t.object_id
                    INNER JOIN sys.columns cp ON fkc.parent_object_id = cp.object_id AND fkc.parent_column_id = cp.column_id
                    INNER JOIN sys.tables rt ON fkc.referenced_object_id = rt.object_id
                    INNER JOIN sys.columns cr ON fkc.referenced_object_id = cr.object_id AND fkc.referenced_column_id = cr.column_id
                    WHERE t.name = @tableName";
                
                using var fkCmd = new SqlCommand(fkQuery, connection);
                fkCmd.Parameters.AddWithValue("@tableName", tableName);
                using var fkAdapter = new SqlDataAdapter(fkCmd);
                var fkData = new DataTable();
                fkAdapter.Fill(fkData);
                
                var foreignKeys = new List<object>();
                foreach (DataRow row in fkData.Rows)
                {
                    foreignKeys.Add(new
                    {
                        name = row["FK_NAME"].ToString(),
                        column = row["PARENT_COLUMN"].ToString(),
                        referencedTable = row["REFERENCED_TABLE"].ToString(),
                        referencedColumn = row["REFERENCED_COLUMN"].ToString()
                    });
                }
                
                schema["tableName"] = tableName;
                schema["columns"] = columns;
                schema["columnCount"] = columns.Count;
                schema["constraints"] = constraints;
                schema["foreignKeys"] = foreignKeys;
            }
            
            return JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
    
    private static bool IsSelectQuery(string sql)
    {
        var normalizedSql = sql.Trim();
        
        var dangerousKeywords = new[] 
        { 
            "INSERT", "UPDATE", "DELETE", "DROP", "CREATE", "ALTER", "TRUNCATE", 
            "EXEC", "EXECUTE", "MERGE", "GRANT", "REVOKE", "DENY"
        };
        
        foreach (var keyword in dangerousKeywords)
        {
            if (Regex.IsMatch(normalizedSql, $@"\b{keyword}\b", RegexOptions.IgnoreCase))
            {
                return false;
            }
        }
        
        if (!Regex.IsMatch(normalizedSql, @"^\s*SELECT\b", RegexOptions.IgnoreCase))
        {
            return false;
        }
        
        return true;
    }
}
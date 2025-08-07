using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using System.ComponentModel;
using Microsoft.Data.SqlClient;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();

[McpServerToolType]
public static class MicrovellumTools
{
    [McpServerTool, Description("A simple hello world tool to test connectivity")]
    public static string Hello(string? name = null) 
        => $"Hello, {name ?? "World"}! This is the Microvellum MCP Server responding.";

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
}
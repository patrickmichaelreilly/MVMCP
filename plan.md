# Microvellum MCP Server Development Plan

## Project Goal
Create an MCP server in C# that enables Claude Code to analyze and modify Microvellum parametric formulas stored in SQL Server. The MCP will provide data access and query tools, while Claude Code will perform higher-level analysis and coordinate modification strategies through conversation.

## Initial Setup
1. Create new GitHub repository named `MVMCP`
2. Initialize with README and .gitignore for C#/.NET projects

## Phase 1: Foundation
1. Create new C# console application project in MVMCP repo
2. Install ModelContextProtocol C# SDK from NuGet
3. Implement basic MCP server with hello-world tool to verify connection
4. Configure claude-code-config.json to register the MCP server
5. Test basic connectivity between Claude Code and MCP server

## Phase 2: Database Connection
1. Add SQL Server connection infrastructure
2. Implement connection string management (local vs network)
3. Create `create_sandbox` tool that:
   - Backs up production database from network
   - Restores to local SQL Server instance with timestamped name
   - Returns connection details for new sandbox
4. Add `list_sandboxes` and `delete_sandbox` tools for management
5. Implement connection switching between sandboxes

## Phase 3: Data Access Tools
1. Create `query` tool for arbitrary SQL execution with result formatting
2. Implement `get_schema` tool to retrieve:
   - Table structures and columns
   - Primary/foreign key relationships
   - Data types and constraints
3. Add `get_formulas` tool with flexible filtering:
   - By table/column
   - By product ID/type
   - By parameter name
   - With pagination for large result sets
4. Create `get_related_data` tool to fetch connected records across tables

## Phase 4: Search and Analysis Tools
1. Implement `search_formulas` tool:
   - Text search across formula content
   - Regex pattern support
   - Return context (product, parameter, full formula)
2. Create `get_parameter_usage` tool:
   - Find where specific parameters are referenced
   - Track default vs. non-default values
3. Add `get_formula_references` tool:
   - Identify all parameters referenced within a formula
   - Map dependencies between formulas

## Phase 5: Modification Tools
1. Create `execute_update` tool:
   - Run UPDATE statements on sandbox
   - Return affected row count
   - Capture before/after values
2. Implement `bulk_update` tool:
   - Execute multiple updates in transaction
   - Provide rollback capability
3. Add `backup_table` tool for creating table snapshots before modifications
4. Create `compare_formulas` tool to show differences after updates

## Phase 6: Utility Tools
1. Add `export_results` tool to save query results to CSV/JSON
2. Implement `get_statistics` tool:
   - Formula count by product type
   - Complexity metrics (length, nesting depth)
   - Parameter usage frequency
3. Create `log_operation` tool to track all modifications with timestamps

## Usage Pattern
The MCP server provides data access and basic operations. Claude Code will:
- Use these tools to explore and understand the data structure
- Identify patterns and improvement opportunities through analysis
- Design modification strategies based on user requirements
- Coordinate complex multi-step changes using the basic tools
- Verify results and ensure data integrity

## Success Criteria
- Claude Code can access all Microvellum formula data through MCP tools
- Safe sandbox environment prevents any production data damage
- Complete visibility into formula dependencies and usage patterns
- Ability to execute and verify complex modification strategies
- Full audit trail of all changes made

## Key Constraints
- Never modify production database directly
- MCP provides data access; Claude Code provides intelligence
- All modifications must be traceable and reversible
- Focus analysis on products actually in use
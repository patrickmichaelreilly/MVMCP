# MVMCP - Microvellum MCP Server

An MCP (Model Context Protocol) server for analyzing and modifying Microvellum parametric formulas stored in SQL Server.

## Overview

This MCP server provides Claude Code with tools to:
- Create safe sandbox database environments
- Query and analyze parametric formulas
- Execute controlled modifications
- Track all changes with full audit trails

## Features

### Database Management
- Create timestamped sandbox databases from production backups
- Switch between multiple sandbox environments
- Automatic cleanup of old sandboxes

### Data Access
- Flexible SQL query execution
- Schema exploration tools
- Formula search and filtering
- Parameter usage analysis
- Dependency mapping

### Modification Tools
- Safe UPDATE execution in sandboxes
- Bulk modification support with transactions
- Before/after comparison
- Full rollback capability

## Setup

1. Install prerequisites:
   - .NET 8.0 SDK or later
   - SQL Server (local instance for sandboxes)
   - Claude Code with MCP support

2. Build the project:
   ```bash
   dotnet build
   ```

3. Configure claude-code-config.json to register the MCP server

4. Run the server:
   ```bash
   dotnet run
   ```

## Architecture

The MCP server acts as a data access layer between Claude Code and SQL Server:
- **MCP Server**: Provides low-level database operations and safety controls
- **Claude Code**: Performs high-level analysis and coordinates modification strategies
- **SQL Server**: Stores production data and isolated sandbox environments

## Safety Features

- Never modifies production database directly
- All changes occur in timestamped sandbox copies
- Complete audit trail of modifications
- Transaction support with rollback capability
- Before/after value capture for all updates

## Development Phases

See [plan.md](plan.md) for the detailed development roadmap.
#!/usr/bin/env python3
"""Development PostgreSQL inspection and archives; never put passwords in argv."""

from __future__ import annotations

import argparse
import csv
from datetime import datetime, timezone
import io
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
from urllib.parse import parse_qsl, unquote, urlsplit
import uuid
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[1]
CONNECTION_KEY = "ConnectionStrings__DefaultConnection"
REQUIRED_TABLES = ("users", "categories", "products", "inventory", "stock_movements")


class DatabaseError(Exception):
    """An error whose message does not contain connection secrets."""


def read_connection(use_user_secrets: bool, api_project: Path) -> tuple[str, str]:
    connection = os.environ.get(CONNECTION_KEY)
    if connection and connection.strip():
        return connection, "environment"
    if not use_user_secrets:
        raise DatabaseError(
            f"Set {CONNECTION_KEY}, or opt in to Development user secrets with --use-user-secrets."
        )
    environments = [os.environ.get(name) for name in ("DOTNET_ENVIRONMENT", "ASPNETCORE_ENVIRONMENT")]
    if any(value and value.lower() != "development" for value in environments):
        raise DatabaseError("User-secrets access is restricted to the Development environment.")
    try:
        project = ET.parse(api_project).getroot()
        secret_id = next(
            (node.text.strip() for node in project.iter() if node.tag.split("}")[-1] == "UserSecretsId" and node.text),
            None,
        )
        if not secret_id or not re.fullmatch(r"[A-Za-z0-9_.-]+", secret_id):
            raise DatabaseError("The API project needs a valid UserSecretsId.")
        if os.name == "nt":
            base = Path(os.environ.get("APPDATA", "")) / "Microsoft" / "UserSecrets"
        else:
            base = Path.home() / ".microsoft" / "usersecrets"
        values = json.loads((base / secret_id / "secrets.json").read_text(encoding="utf-8-sig"))
        connection = values.get("ConnectionStrings:DefaultConnection")
        if connection is None and isinstance(values.get("ConnectionStrings"), dict):
            connection = values["ConnectionStrings"].get("DefaultConnection")
        if not isinstance(connection, str) or not connection.strip():
            raise DatabaseError("Development user secrets do not contain ConnectionStrings:DefaultConnection.")
        return connection, "Development user secrets"
    except (OSError, ET.ParseError, ValueError, AttributeError, TypeError):
        raise DatabaseError("Could not read the API Development user-secrets configuration.") from None


def npgsql_pairs(connection: str) -> list[tuple[str, str]]:
    """Parse semicolon key/value pairs, including quoted values and doubled quotes."""
    pairs: list[tuple[str, str]] = []
    cursor = 0
    while cursor < len(connection):
        while cursor < len(connection) and (connection[cursor].isspace() or connection[cursor] == ";"):
            cursor += 1
        if cursor == len(connection):
            break
        start = cursor
        while cursor < len(connection) and connection[cursor] not in "=;":
            cursor += 1
        if cursor == len(connection) or connection[cursor] != "=":
            raise DatabaseError("Invalid Npgsql connection-string syntax.")
        key = re.sub(r"[\s_]", "", connection[start:cursor]).lower()
        cursor += 1
        while cursor < len(connection) and connection[cursor].isspace():
            cursor += 1
        if cursor < len(connection) and connection[cursor] in "\"'":
            quote = connection[cursor]
            cursor += 1
            value = ""
            while True:
                if cursor >= len(connection):
                    raise DatabaseError("Unterminated quoted connection-string value.")
                if connection[cursor] == quote:
                    cursor += 1
                    if cursor < len(connection) and connection[cursor] == quote:
                        value += quote
                        cursor += 1
                        continue
                    break
                value += connection[cursor]
                cursor += 1
            while cursor < len(connection) and connection[cursor].isspace():
                cursor += 1
            if cursor < len(connection) and connection[cursor] != ";":
                raise DatabaseError("Unexpected characters after a quoted connection-string value.")
        else:
            start = cursor
            while cursor < len(connection) and connection[cursor] != ";":
                cursor += 1
            value = connection[start:cursor].strip()
        pairs.append((key, value))
    return pairs


def connection_environment(connection: str) -> dict[str, str]:
    parameters: dict[str, str] = {}
    aliases = {
        "host": "PGHOST", "server": "PGHOST", "port": "PGPORT",
        "database": "PGDATABASE", "initialcatalog": "PGDATABASE",
        "username": "PGUSER", "userid": "PGUSER", "user": "PGUSER",
        "password": "PGPASSWORD", "pwd": "PGPASSWORD", "sslmode": "PGSSLMODE",
        "timeout": "PGCONNECT_TIMEOUT", "connectiontimeout": "PGCONNECT_TIMEOUT",
        "applicationname": "PGAPPNAME", "rootcertificate": "PGSSLROOTCERT",
        "sslcertificate": "PGSSLCERT", "sslkey": "PGSSLKEY",
    }
    if connection.lower().startswith(("postgresql://", "postgres://")):
        try:
            parsed = urlsplit(connection)
            if parsed.fragment or not parsed.hostname:
                raise ValueError
            parameters["PGHOST"] = unquote(parsed.hostname)
            parameters["PGDATABASE"] = unquote(parsed.path.lstrip("/"))
            if parsed.username is not None:
                parameters["PGUSER"] = unquote(parsed.username)
            if parsed.password is not None:
                parameters["PGPASSWORD"] = unquote(parsed.password)
            if parsed.port is not None:
                parameters["PGPORT"] = str(parsed.port)
            url_keys = {
                "sslmode": "PGSSLMODE", "connect_timeout": "PGCONNECT_TIMEOUT",
                "application_name": "PGAPPNAME", "sslrootcert": "PGSSLROOTCERT",
                "sslcert": "PGSSLCERT", "sslkey": "PGSSLKEY",
            }
            for key, value in parse_qsl(parsed.query, keep_blank_values=True, strict_parsing=True):
                if key not in url_keys or url_keys[key] in parameters:
                    raise DatabaseError("Unsupported or duplicate PostgreSQL URL option.")
                parameters[url_keys[key]] = value
        except ValueError:
            raise DatabaseError("Invalid PostgreSQL URL; provide a single explicit database target.") from None
    else:
        for key, value in npgsql_pairs(connection):
            if key not in aliases:
                raise DatabaseError("Unsupported Npgsql option; this helper accepts connection options only.")
            name = aliases[key]
            if name in parameters:
                raise DatabaseError("Duplicate connection-string option.")
            parameters[name] = value
        if "PGSSLMODE" in parameters:
            modes = {"disable": "disable", "allow": "allow", "prefer": "prefer", "require": "require",
                     "verifyca": "verify-ca", "verifyfull": "verify-full"}
            mode = re.sub(r"[\s_-]", "", parameters["PGSSLMODE"]).lower()
            if mode not in modes:
                raise DatabaseError("Unsupported SSL mode.")
            parameters["PGSSLMODE"] = modes[mode]
    if any(not parameters.get(key) for key in ("PGHOST", "PGDATABASE", "PGUSER")):
        raise DatabaseError("Provide an explicit host, database and username.")
    if any("\x00" in value for value in parameters.values()):
        raise DatabaseError("Connection options cannot contain NUL characters.")
    if "," in parameters["PGHOST"] or "=" in parameters["PGDATABASE"] or "://" in parameters["PGDATABASE"]:
        raise DatabaseError("Multiple hosts and connection strings embedded in database names are unsupported.")
    parameters.setdefault("PGPORT", "5432")
    if not parameters["PGPORT"].isdigit() or not 1 <= int(parameters["PGPORT"]) <= 65535:
        raise DatabaseError("Port must be an integer from 1 to 65535.")
    parameters.setdefault("PGCONNECT_TIMEOUT", "10")
    if not parameters["PGCONNECT_TIMEOUT"].isdigit() or int(parameters["PGCONNECT_TIMEOUT"]) < 1:
        raise DatabaseError("Connection timeout must be a positive integer.")
    if parameters.get("PGSSLMODE", "prefer") not in ("disable", "allow", "prefer", "require", "verify-ca", "verify-full"):
        raise DatabaseError("Unsupported SSL mode.")
    parameters.setdefault("PGAPPNAME", "smart-inventory-dev-db")
    # Avoid inherited libpq service/options changing the selected target or query behavior.
    environment = {key: value for key, value in os.environ.items() if not key.upper().startswith("PG")}
    environment.update(parameters)
    return environment


def tool_path(name: str) -> str:
    found = shutil.which(name)
    fallback = Path("/Library/PostgreSQL/18/bin") / name
    if found:
        return found
    if fallback.is_file() and os.access(fallback, os.X_OK):
        return str(fallback)
    raise DatabaseError(f"PostgreSQL tool {name} was not found on PATH or /Library/PostgreSQL/18/bin.")


def redact(message: str, connection: str, environment: dict[str, str]) -> str:
    for value in (connection, environment.get("PGPASSWORD", "")):
        if value:
            message = message.replace(value, "[REDACTED]")
    return message.strip()


def run_psql(sql: str, environment: dict[str, str], connection: str) -> str:
    result = subprocess.run(
        [tool_path("psql"), "-X", "--no-password", "--quiet", "--csv", "--tuples-only", "--set=ON_ERROR_STOP=1"],
        input=sql, text=True, capture_output=True, env=environment, timeout=60, check=False,
    )
    if result.returncode:
        raise DatabaseError("PostgreSQL command failed: " + redact(result.stderr, connection, environment))
    return result.stdout


def inspect_database(environment: dict[str, str], connection: str) -> int:
    checks = [
        ("duplicate_product_sku", {"products": ("sku",)}, "SELECT COUNT(*) FROM (SELECT sku FROM public.products GROUP BY sku HAVING COUNT(*) > 1) duplicates"),
        ("duplicate_username", {"users": ("username",)}, "SELECT COUNT(*) FROM (SELECT username FROM public.users GROUP BY username HAVING COUNT(*) > 1) duplicates"),
        ("orphan_product_category", {"products": ("category_id",), "categories": ("id",)}, "SELECT COUNT(*) FROM public.products p LEFT JOIN public.categories c ON c.id = p.category_id WHERE c.id IS NULL"),
        ("orphan_inventory_product", {"inventory": ("product_id",), "products": ("id",)}, "SELECT COUNT(*) FROM public.inventory i LEFT JOIN public.products p ON p.id = i.product_id WHERE p.id IS NULL"),
        ("orphan_movement_product", {"stock_movements": ("product_id",), "products": ("id",)}, "SELECT COUNT(*) FROM public.stock_movements m LEFT JOIN public.products p ON p.id = m.product_id WHERE p.id IS NULL"),
        ("negative_inventory", {"inventory": ("quantity",)}, "SELECT COUNT(*) FROM public.inventory WHERE quantity < 0"),
        ("negative_product_price", {"products": ("price",)}, "SELECT COUNT(*) FROM public.products WHERE price < 0"),
        ("negative_min_stock", {"products": ("min_stock",)}, "SELECT COUNT(*) FROM public.products WHERE min_stock < 0"),
        ("negative_movement_quantity", {"stock_movements": ("quantity",)}, "SELECT COUNT(*) FROM public.stock_movements WHERE quantity < 0"),
    ]
    statements = ["BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY;", "SET LOCAL statement_timeout = '15s';"]
    for table in (*REQUIRED_TABLES, "__EFMigrationsHistory"):
        statements.append(f"SELECT 'schema', '{table}', CASE WHEN to_regclass('public.\"{table}\"') IS NULL THEN 'missing' ELSE 'present' END;")
        query = f"SELECT 'rows', '{table}', COUNT(*)::text FROM public.\"{table}\";"
        statements.append(f"SELECT {sql_literal(query)} WHERE to_regclass('public.\"{table}\"') IS NOT NULL\n\\gexec")
    migration_query = 'SELECT \'migration\', "MigrationId"::text, "ProductVersion"::text FROM public."__EFMigrationsHistory" ORDER BY "MigrationId";'
    statements.append(f"SELECT {sql_literal(migration_query)} WHERE to_regclass('public.\"__EFMigrationsHistory\"') IS NOT NULL\n\\gexec")
    for name, tables, query in checks:
        conditions = []
        for table, columns in tables.items():
            names = ",".join(sql_literal(column) for column in columns)
            conditions.append(f"(SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'public' AND table_name = '{table}' AND column_name IN ({names})) = {len(columns)}")
        condition = " AND ".join(conditions)
        check_query = f"SELECT 'check', '{name}', ({query})::text;"
        statements.append(f"SELECT CASE WHEN {condition} THEN {sql_literal(check_query)} ELSE {sql_literal(f'SELECT \'check\', \'{name}\', \'skipped_missing_columns\';')} END\n\\gexec")
    statements.append("COMMIT;")
    output = run_psql("\n".join(statements), environment, connection)
    failed = False
    for row in csv.reader(io.StringIO(output)):
        if not row:
            continue
        if len(row) != 3:
            raise DatabaseError("Unexpected inspection result format.")
        section, name, value = row
        print(redact(f"{section:10} {name}: {value}", connection, environment))
        failed |= (section == "schema" and value == "missing") or (section == "check" and value != "0")
    print("Preflight requires attention." if failed else "Read-only preflight completed without detected data anomalies.")
    return 2 if failed else 0


def sql_literal(value: str) -> str:
    return "'" + value.replace("'", "''") + "'"


def configured_backup_directory(configured: str | None = None) -> Path:
    """Return the workspace-level backup folder, honoring an optional local override."""
    if configured is None:
        configured = os.environ.get("AI_SMART_INVENTORY_BACKUP_DIR")
    directory = Path(configured).expanduser() if configured else ROOT.parent / "backups"
    if not directory.is_absolute():
        directory = ROOT.parent / directory
    if directory.is_symlink():
        raise DatabaseError("The backup directory must not be a symbolic link.")
    directory.mkdir(mode=0o700, parents=True, exist_ok=True)
    return directory.resolve()


def backup_target_path(backup_directory: Path, output: Path | None, filename: str) -> Path:
    """Resolve an archive filename and prevent writing outside the chosen folder."""
    requested = output if output is not None else Path(filename)
    if requested.is_absolute() or len(requested.parts) != 1 or requested.name in ("", ".", ".."):
        raise DatabaseError("Backup output must be a filename directly inside the backup directory.")
    target = (backup_directory / requested).resolve()
    if not target.is_relative_to(backup_directory) or target.parent != backup_directory:
        raise DatabaseError("Backup output must be a file directly inside the backup directory.")
    return target


def backup_database(output: Path | None, environment: dict[str, str], connection: str) -> int:
    backup_directory = configured_backup_directory()
    filename = f"ai-smart-inventory-{datetime.now(timezone.utc):%Y%m%dT%H%M%SZ}-{uuid.uuid4().hex[:8]}.dump"
    target = backup_target_path(backup_directory, output, filename)
    executable = tool_path("pg_dump")
    descriptor = os.open(target, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    try:
        with os.fdopen(descriptor, "wb") as archive:
            result = subprocess.run(
                [executable, "--format=custom", "--no-password", "--no-owner", "--no-acl"],
                stdout=archive, stderr=subprocess.PIPE, env=environment, timeout=900, check=False,
            )
        if result.returncode:
            raise DatabaseError("Backup failed: " + redact(result.stderr.decode(errors="replace"), connection, environment))
    except BaseException:
        target.unlink(missing_ok=True)  # Only remove the incomplete archive created by this invocation.
        raise
    print(f"Custom-format backup saved: {target}")
    return 0


def restore_database(archive: Path, environment: dict[str, str], connection: str) -> int:
    archive = archive.resolve()
    if not archive.is_file():
        raise DatabaseError("Restore archive does not exist or is not a regular file.")
    # A successful connection proves that the configured target already exists.
    run_psql("BEGIN READ ONLY; SELECT 1; COMMIT;", environment, connection)
    result = subprocess.run(
        [tool_path("pg_restore"), "--no-password", "--no-owner", "--no-acl", "--exit-on-error",
         "--single-transaction", "--dbname", environment["PGDATABASE"], str(archive)],
        text=True, capture_output=True, env=environment, timeout=900, check=False,
    )
    if result.returncode:
        raise DatabaseError("Restore failed; its transaction was rolled back: " + redact(result.stderr, connection, environment))
    print("Archive restored into the configured existing database in one transaction.")
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--use-user-secrets", action="store_true", help="Use API Development user secrets when the connection environment variable is absent.")
    parser.add_argument("--api-project", type=Path, default=ROOT / "backend/SmartInventory.Api/SmartInventory.Api.csproj")
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("inspect", help="Read-only row counts, migration history and data preflight.")
    backup = commands.add_parser("backup", help="Create an exclusive custom-format archive under the workspace backups/ directory.")
    backup.add_argument("--output", type=Path, help="Archive filename inside the backup directory; existing files are never overwritten.")
    restore = commands.add_parser("restore", help="Restore to an existing configured database, without drop/create.")
    restore.add_argument("archive", type=Path)
    restore.add_argument("--confirm-restore", action="store_true", help="Explicitly authorize writes to the configured restore target.")
    arguments = parser.parse_args(argv)
    try:
        if arguments.command == "restore" and not arguments.confirm_restore:
            raise DatabaseError("Restore requires --confirm-restore; no database command was executed.")
        connection, source = read_connection(arguments.use_user_secrets, arguments.api_project)
        environment = connection_environment(connection)
        print(f"Connection loaded from {source}; credentials are not printed.")
        if arguments.command == "inspect":
            return inspect_database(environment, connection)
        if arguments.command == "backup":
            return backup_database(arguments.output, environment, connection)
        return restore_database(arguments.archive, environment, connection)
    except DatabaseError as error:
        print(f"Error: {error}", file=sys.stderr)
    except FileExistsError:
        print("Error: Archive exists; choose a new backup filename.", file=sys.stderr)
    except subprocess.TimeoutExpired:
        print("Error: PostgreSQL command timed out; no connection details are printed.", file=sys.stderr)
    except OSError:
        print("Error: Could not access a PostgreSQL tool or archive file.", file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main())

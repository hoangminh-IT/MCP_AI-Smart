#!/usr/bin/env python3
"""Run React -> API -> PostgreSQL browser checks in a disposable, private test schema."""
from __future__ import annotations

import importlib.util
import os
from pathlib import Path
import secrets
import signal
import socket
import subprocess
import sys
import tempfile
import time
from urllib.error import URLError
from urllib.request import urlopen
import uuid

ROOT = Path(__file__).resolve().parents[1]


def port() -> int:
    with socket.socket() as listener:
        listener.bind(("127.0.0.1", 0))
        return listener.getsockname()[1]


def ready(url: str, process: subprocess.Popen) -> None:
    for _ in range(150):
        if process.poll() is not None:
            raise RuntimeError("A smoke-test server exited before becoming ready.")
        try:
            with urlopen(url, timeout=1) as response:
                if response.status == 200:
                    return
        except (URLError, TimeoutError):
            pass
        time.sleep(0.2)
    raise RuntimeError("A smoke-test server did not become ready within 30 seconds.")


def main() -> int:
    specification = importlib.util.spec_from_file_location("dev_db", ROOT / "scripts/dev-db.py")
    database = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(database)
    connection, _ = database.read_connection(True, ROOT / "backend/SmartInventory.Api/SmartInventory.Api.csproj")
    if connection.lower().startswith(("postgres://", "postgresql://")):
        raise RuntimeError("Browser tests require an Npgsql connection string, as used by the C# API.")
    pg_environment = database.connection_environment(connection)
    schema = "inventory_browser_" + uuid.uuid4().hex
    api_port, web_port, mcp_port = port(), port(), port()
    while web_port == api_port:
        web_port = port()
    api_url, web_url = f"http://127.0.0.1:{api_port}", f"http://127.0.0.1:{web_port}"
    mcp_url = f"http://127.0.0.1:{mcp_port}"
    environment = os.environ.copy()
    environment.update({
        "ConnectionStrings__DefaultConnection": connection.rstrip(";") + ";Search Path=" + schema,
        "ASPNETCORE_ENVIRONMENT": "Development", "DOTNET_ENVIRONMENT": "Development",
        "Jwt__SigningKey": secrets.token_urlsafe(48),
        "Jwt__Issuer": "browser-smoke", "Jwt__Audience": "browser-smoke",
        "Bootstrap__Username": "browser_admin", "Bootstrap__Password": secrets.token_urlsafe(24),
        "Bootstrap__Email": "browser-admin@example.test",
        "VITE_API_PROXY_TARGET": api_url, "E2E_BASE_URL": web_url,
        "Mcp__ServerUrl": mcp_url + "/mcp", "Mcp__UseSharedFirstPartyAudience": "true",
        "Llm__ApiKey": "",
        "E2E_USERNAME": "browser_admin",
    })
    environment["E2E_PASSWORD"] = environment["Bootstrap__Password"]
    chrome = Path("/Applications/Google Chrome.app/Contents/MacOS/Google Chrome")
    if not environment.get("PLAYWRIGHT_CHROMIUM_EXECUTABLE") and chrome.is_file():
        environment["PLAYWRIGHT_CHROMIUM_EXECUTABLE"] = str(chrome)
    api_project = "backend/SmartInventory.Api"
    processes = []
    created = False
    try:
        database.run_psql(f'CREATE SCHEMA "{schema}";', pg_environment, connection)
        created = True
        with tempfile.TemporaryDirectory(prefix="inventory-browser-") as temporary:
            environment["ProductImages__RootPath"] = str(Path(temporary) / "images")
            with open(Path(temporary) / "servers.log", "w+") as output:
                def run(command: list[str], cwd: Path = ROOT) -> None:
                    completed = subprocess.run(command, cwd=cwd, env=environment, stdout=output, stderr=output, timeout=120)
                    if completed.returncode:
                        raise RuntimeError("Smoke-test setup command failed; verify the solution build and development configuration.")

                run(["dotnet", "ef", "database", "update", "--no-build", "--project", "backend/SmartInventory.Infrastructure", "--startup-project", api_project, "--context", "AppDbContext"])
                run(["dotnet", "run", "--no-build", "--no-launch-profile", "--project", api_project, "--", "--bootstrap-admin"])
                processes.append(subprocess.Popen(["dotnet", str(ROOT / "backend/SmartInventory.McpServer/bin/Debug/net10.0/SmartInventory.McpServer.dll"), "--urls", mcp_url], cwd=ROOT, env=environment, stdout=output, stderr=output, start_new_session=True))
                ready(mcp_url + "/health/live", processes[-1])
                # The browser creates its own business records through the UI; no mocked requests.
                processes.append(subprocess.Popen(["dotnet", "run", "--no-build", "--no-launch-profile", "--project", api_project, "--", "--urls", api_url], cwd=ROOT, env=environment, stdout=output, stderr=output, start_new_session=True))
                ready(api_url + "/health/live", processes[-1])
                processes.append(subprocess.Popen(["npm", "run", "dev", "--", "--port", str(web_port), "--strictPort"], cwd=ROOT / "frontend/frontend", env=environment, stdout=output, stderr=output, start_new_session=True))
                ready(web_url, processes[-1])
                result = subprocess.run(["npm", "test"], cwd=ROOT / "tests/browser", env=environment, timeout=180)
                return result.returncode
    finally:
        for process in reversed(processes):
            try:
                os.killpg(process.pid, signal.SIGTERM)
            except ProcessLookupError:
                continue
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                os.killpg(process.pid, signal.SIGKILL)
                process.wait(timeout=5)
        if created:
            # Only the fresh UUID schema created above is removed, never public or the database itself.
            database.run_psql(f'DROP SCHEMA "{schema}" CASCADE;', pg_environment, connection)


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as error:
        # Underlying connection/configuration exceptions must never expose the raw DSN.
        if isinstance(error, RuntimeError):
            print(str(error), file=sys.stderr)
        else:
            print("Browser check could not run. Check local prerequisites and PostgreSQL configuration.", file=sys.stderr)
        sys.exit(1)

# Smart Inventory frontend

React 19 + TypeScript + Vite 7, Material UI, React Router, Axios and TanStack Query. Dependency versions are exact and `package-lock.json` is committed. Requires Node.js 20.19+ or 22.12+; verified with Node 24.

```sh
npm ci
npm run dev
```

The development server listens at `http://127.0.0.1:5173`. Start the ASP.NET Core API separately at `http://localhost:5000`; `/api` and `/uploads` are proxied to it. To use a different API port:

```sh
VITE_API_PROXY_TARGET=http://127.0.0.1:5062 npm run dev
```

This variable contains a public development API address, never a token or API key. The browser always calls relative `/api` paths. `npm run build` type-checks and builds static production files into `dist/`. A production reverse proxy must route `/api` and `/uploads` to ASP.NET Core and serve `index.html` for React routes. Vite preview alone does not provide a production API proxy.

The existing workspace name contains `#`, which Vite normally treats as a URL delimiter. `scripts/vite.mjs` uses a temporary clean symlink during dev/build and removes it when the process exits. Source, dependencies and build output remain in the existing checkout. Use the npm scripts rather than invoking Vite directly. The dev script accepts `--host`, `--port` and `--strictPort`.

Implemented pages:

- Login and session expiry handling with server validation through `/auth/me`.
- Products: real server pagination, search, category/inactive filters, sorting, detail drawer, Admin create/edit/deactivate, CSV import/export and JPG/PNG/WebP image upload.
- Categories: real server pagination/search/sort and Admin CRUD; blocked deletion errors remain visible.
- Profile: update full name/email, change password and return to login; confirmed logout.
- MCP Client Lab (`/mcp/lab`): live API status, read-only server connection settings, manual SDK-backed ListTools, tool schemas/annotations and a C# client example.
- MCP Tool Playground (`/mcp/playground`): select an actual discovered tool, edit JSON arguments, inspect the request and execute through the C# backend. HTTP errors and MCP tool errors are shown separately with the full protocol result and measured duration.
- MCP Execution Trace (`/mcp/trace`): current user's paginated execution history, saved input/output and observed total execution time. It does not invent separate database or LLM spans.
- AI Assistant (`/ai`): real own-user conversation history with create/rename/delete, provider configuration status, persisted messages, collapsible MCP records and observed Agent Trace. Each MCP Agent Trace tool span includes the observed tool name, sanitized input/output JSON, status, duration and execution ID. Sending is disabled when the backend reports an unconfigured provider; the independent MCP screens remain available. A live LLM response requires backend provider configuration and has not been claimed by a frontend build.

Staff accounts have read access to Products/Categories and can update their own profile/password. The API independently enforces authorization. MCP Direct Test Mode calls the real C# MCP client/server without an LLM key; it never represents a direct tool response as an AI answer. Tools explicitly annotated as writes are blocked until an application confirmation workflow is implemented. Connection status is polled from `/api/mcp/status` every ten seconds while the screen is active. The browser cannot choose an arbitrary server URL.

The C# example in Client Lab uses the verified SDK 2.3.0 transport, `McpClient.CreateAsync`, `ListToolsAsync` and `CallToolAsync` APIs with bounded cancellation and disposal. It uses a caller-token variable, never a sample JWT value. AI settings are backend-only: no API-key or provider-base-URL form exists in the browser. Chat requests do not automatically retry and can be cancelled; persisted history and errors remain visible after failures.

Access tokens live in memory and `sessionStorage` so a page reload can resume a session within its tab. `sessionStorage` is readable by JavaScript and is vulnerable to XSS; this is a development tradeoff rather than a secure cookie design. No password is stored; tokens and cached data are cleared on logout, expiry or a server `401`. A hardened deployment should evaluate an HTTP-only secure-cookie flow and a restrictive content security policy.

The production build has been executed successfully. The root repository records real API/browser integration results separately; this frontend does not claim an independent unit-test suite.

import { mkdtempSync, rmSync, symlinkSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawn } from 'node:child_process';

// Vite treats # and ? as URL delimiters. Keep the existing checkout intact and
// resolve application files through a temporary alias when its path contains one.
const project = dirname(dirname(fileURLToPath(import.meta.url)));
let temporaryDirectory;
let root = project;
if (/[#?]/.test(project)) {
  temporaryDirectory = mkdtempSync(join(tmpdir(), 'smart-inventory-vite-'));
  root = join(temporaryDirectory, 'project');
  symlinkSync(project, root, process.platform === 'win32' ? 'junction' : 'dir');
}
const cleanup = () => {
  if (temporaryDirectory) {
    rmSync(temporaryDirectory, { recursive: true, force: true });
    temporaryDirectory = undefined;
  }
};
process.on('exit', cleanup);
const [command = 'dev', ...arguments_] = process.argv.slice(2);
// The Vite package itself also contains the checkout path in its injected
// /@vite/client module. Start it through the alias before importing Vite so both
// the application and Vite's runtime retain paths without URL delimiters.
if (temporaryDirectory) {
  const child = spawn(process.execPath, [
    '--preserve-symlinks', '--preserve-symlinks-main',
    join(root, 'scripts/vite.mjs'), command, ...arguments_,
  ], { cwd: root, env: process.env, stdio: 'inherit' });
  for (const signal of ['SIGINT', 'SIGTERM']) process.once(signal, () => child.kill(signal));
  const exitCode = await new Promise((resolve, reject) => {
    child.once('error', reject);
    child.once('exit', (code, signal) => resolve(code ?? (signal === 'SIGINT' ? 130 : 1)));
  });
  cleanup();
  process.exit(exitCode);
}
const { build, createServer, preview } = await import('vite');
const config = { root, configFile: join(root, 'vite.config.ts'), resolve: { preserveSymlinks: true } };

function option(name, fallback) {
  const position = arguments_.indexOf(name);
  if (position >= 0) return arguments_[position + 1] ?? fallback;
  return arguments_.find((value) => value.startsWith(`${name}=`))?.slice(name.length + 1) ?? fallback;
}

try {
  if (command === 'build') {
    await build(config);
    cleanup();
  } else {
    const host = option('--host', '127.0.0.1');
    const port = Number(option('--port', command === 'preview' ? '4173' : '5173'));
    if (!Number.isInteger(port) || port < 1 || port > 65535) throw new Error('Port must be between 1 and 65535.');
    const server = command === 'preview'
      ? await preview({ ...config, preview: { host, port, strictPort: arguments_.includes('--strictPort') } })
      : await createServer({ ...config, server: { host, port, strictPort: arguments_.includes('--strictPort') } });
    if (command !== 'preview') await server.listen();
    server.printUrls();
    for (const signal of ['SIGINT', 'SIGTERM']) {
      process.once(signal, async () => {
        if ('close' in server) await server.close();
        else await new Promise((resolve) => server.httpServer.close(resolve));
        cleanup();
        process.exit(0);
      });
    }
  }
} catch (error) {
  cleanup();
  console.error(error);
  process.exitCode = 1;
}

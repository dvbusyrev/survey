import { build, context } from 'esbuild';
import { mkdir, rm } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const isWatchMode = process.argv.includes('--watch');
const currentFile = fileURLToPath(import.meta.url);
const rootDir = path.resolve(path.dirname(currentFile), '..');
const outdir = path.join(rootDir, 'wwwroot/js/dist');

const buildOptions = {
  absWorkingDir: rootDir,
  bundle: true,
  charset: 'utf8',
  define: {
    'process.env.NODE_ENV': '"production"'
  },
  entryPoints: {
    'admin-inline-app': 'frontend/entries/admin-inline-app.js',
    'auth-page': 'frontend/entries/auth-page.js',
    'survey-fill-app': 'frontend/entries/survey-fill-app.js',
    'survey-user-app': 'frontend/entries/survey-user-app.js'
  },
  format: 'iife',
  inject: [
    path.join(rootDir, 'frontend/shims/react-globals.js')
  ],
  loader: {
    '.js': 'jsx',
    '.jsx': 'jsx'
  },
  logLevel: 'info',
  outdir,
  platform: 'browser',
  sourcemap: true,
  target: ['es2020']
};

await rm(outdir, { force: true, recursive: true });
await mkdir(outdir, { recursive: true });

if (isWatchMode) {
  const watchContext = await context(buildOptions);
  await watchContext.watch();
  console.log('Watching frontend sources...');
} else {
  await build(buildOptions);
}

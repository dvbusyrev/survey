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
    'answer-statistics-page': 'wwwroot/js/entries/answer-statistics-page.js',
    'auth-page': 'wwwroot/js/entries/auth-page.js',
    'check-answers-app': 'wwwroot/js/entries/check-answers-app.js',
    'survey-fill-app': 'wwwroot/js/entries/survey-fill-app.js',
    'survey-user-app': 'wwwroot/js/entries/survey-user-app.js'
  },
  format: 'iife',
  loader: {
    '.js': 'js'
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

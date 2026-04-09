import { readdir, readFile, stat } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const currentFile = fileURLToPath(import.meta.url);
const rootDir = path.resolve(path.dirname(currentFile), '..');

const rootsToScan = [
  path.join(rootDir, 'Web/wwwroot/js/entries'),
  path.join(rootDir, 'Web/wwwroot/js/features'),
  path.join(rootDir, 'Web/wwwroot/js/ui'),
  path.join(rootDir, 'Web/wwwroot/js/core')
];

const allowedExtensions = new Set(['.js']);
const violations = [];

function toRelative(targetPath) {
  return path.relative(rootDir, targetPath).split(path.sep).join('/');
}

async function collectFiles(targetPath) {
  const info = await stat(targetPath);
  if (info.isFile()) {
    return [targetPath];
  }

  const entries = await readdir(targetPath, { withFileTypes: true });
  const nested = await Promise.all(
    entries.map(async (entry) => {
      const entryPath = path.join(targetPath, entry.name);
      if (entry.isDirectory()) {
        if (entry.name === 'dist' || entry.name === 'node_modules') {
          return [];
        }
        return collectFiles(entryPath);
      }
      return [entryPath];
    })
  );

  return nested.flat();
}

function getLineNumber(source, offset) {
  return source.slice(0, offset).split('\n').length;
}

function collectInnerHtmlViolations(source) {
  const matches = [];
  const templateAssignments = /\binnerHTML\b\s*=\s*`([\s\S]*?)`/g;
  const singleQuotedAssignments = /\binnerHTML\b\s*=\s*'([^']*)'/g;
  const doubleQuotedAssignments = /\binnerHTML\b\s*=\s*"([^"]*)"/g;

  const scan = (regex) => {
    let match = regex.exec(source);
    while (match) {
      const assignedValue = match[1] || '';
      if (assignedValue.includes('<')) {
        matches.push(match.index);
      }
      match = regex.exec(source);
    }
  };

  scan(templateAssignments);
  scan(singleQuotedAssignments);
  scan(doubleQuotedAssignments);

  return matches;
}

for (const rootPath of rootsToScan) {
  const files = await collectFiles(rootPath);
  for (const filePath of files) {
    const ext = path.extname(filePath).toLowerCase();
    if (!allowedExtensions.has(ext)) {
      continue;
    }

    const source = await readFile(filePath, 'utf8');
    const fileViolations = collectInnerHtmlViolations(source);
    if (fileViolations.length === 0) {
      continue;
    }

    fileViolations.forEach((offset) => {
      violations.push({
        file: toRelative(filePath),
        line: getLineNumber(source, offset)
      });
    });
  }
}

if (violations.length > 0) {
  console.error('Found forbidden `innerHTML` HTML injections:');
  violations.forEach((item) => {
    console.error(` - ${item.file}:${item.line}`);
  });
  process.exit(1);
}

console.log('No forbidden innerHTML assignments found.');

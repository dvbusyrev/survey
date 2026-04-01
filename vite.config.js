import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'path';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true
  },
  build: {
    outDir: path.resolve(__dirname, 'wwwroot/dist'),
    emptyOutDir: false,
    rollupOptions: {
      input: {
        auth: path.resolve(__dirname, 'src/entries/auth.jsx')
      },
      output: {
        entryFileNames: '[name].js',
        chunkFileNames: 'chunks/[name].js',
        assetFileNames: 'assets/[name][extname]'
      }
    }
  }
});

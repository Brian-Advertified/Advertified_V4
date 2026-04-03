import { describe, expect, it } from 'vitest';
import fs from 'fs';
import path from 'path';

describe('Dashboard mobile CSS behavior', () => {
  it('ensures user grid and toolbar styles include mobile breakpoints', async () => {
    const cssPath = path.resolve(__dirname, '../src/index.css');
    const css = await fs.promises.readFile(cssPath, 'utf8');

    expect(css).toContain('@media (max-width: 1023px)');
    expect(css).toContain('.user-grid-4');
    expect(css).toContain('grid-template-columns: repeat(1, minmax(0, 1fr));');

    expect(css).toContain('@media (max-width: 767px)');
    expect(css).toContain('.user-toolbar');
    expect(css).toContain('flex-direction: column');
    expect(css).toMatch(/\b\.user-btn[^\{]*\{[^\}]*width:\s*100%/s);
  });
});

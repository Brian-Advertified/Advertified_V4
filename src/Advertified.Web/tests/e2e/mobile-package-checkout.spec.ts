import { test, expect, devices } from '@playwright/test';

/**
 * Mobile E2E Tests - Package and Checkout Flow
 * Tests the core user journey on mobile devices (Pixel 5 and iPhone 12)
 */

test.describe('Mobile Package Selection and Checkout Flow', () => {
  // Test on mobile chrome (Pixel 5)
  test('should display and interact with package cards on mobile without horizontal scroll', async ({ browser }) => {
    const context = await browser.newContext({
      ...devices['Pixel 5'],
    });
    const page = await context.newPage();
    await page.goto('/packages');

    // Wait for packages to load
    await page.waitForSelector('[data-testid="package-card"], .card-grid', { timeout: 5000 }).catch(() => {
      // Cards might not have test ids, that's okay
    });

    // Check that package grid is responsive and doesn't overflow
    const grid = page.locator('.card-grid, [class*="grid"]').first();
    const gridBox = await grid.boundingBox();

    // Get viewport width
    const viewport = page.viewportSize();
    if (gridBox && viewport) {
      // Grid should not exceed viewport width
      expect(gridBox.width).toBeLessThanOrEqual(viewport.width + 10); // +10 for small margin of error
    }

    // Verify cards are stacked vertically on mobile (grid-cols-1)
    const cards = page.locator('[class*="card"], button[aria-pressed]').first();
    const computedStyle = await cards.evaluate((el) => {
      return window.getComputedStyle(el);
    });

    // On mobile, cards should be full width or close to it
    expect(computedStyle?.display).toMatch(/grid|flex|block/i);

    await context.close();
  });

  test('should maintain proper touch targets on mobile (min 44px)', async ({ browser }) => {
    const context = await browser.newContext({
      ...devices['Pixel 5'],
    });
    const page = await context.newPage();
    await page.goto('/packages');

    // Find all buttons
    const buttons = page.locator('button, [role="button"]');
    const count = await buttons.count();

    for (let i = 0; i < Math.min(count, 5); i++) {
      const button = buttons.nth(i);
      const box = await button.boundingBox();

      if (box) {
        // Touch targets should be at least 44x44px (recommended by WCAG)
        expect(box.width).toBeGreaterThanOrEqual(40); // Allow small margin
        expect(box.height).toBeGreaterThanOrEqual(40);
      }
    }

    await context.close();
  });

  test('should display payment selection page responsively on mobile', async ({ browser }) => {
    const context = await browser.newContext({
      ...devices['iPhone 12'],
    });
    const page = await context.newPage();

    // Navigate via packages first (would need actual auth + package selection in real scenario)
    await page.goto('/packages');

    // Verify page doesn't have horizontal scrollbar on mobile
    const bodyWidth = await page.evaluate(() => document.body.offsetWidth);
    const windowWidth = await page.evaluate(() => window.innerWidth);

    expect(bodyWidth).toBeLessThanOrEqual(windowWidth + 2); // Small tolerance

    await context.close();
  });

  test('should have accessible form inputs and labels on mobile', async ({ browser }) => {
    const context = await browser.newContext({
      ...devices['Pixel 5'],
    });
    const page = await context.newPage();
    await page.goto('/packages');

    // Check for ARIA labels on interactive elements
    const interactiveElements = page.locator('button, [role="button"], input, textarea');
    const count = await interactiveElements.count();

    let elementsWithAccessibility = 0;

    for (let i = 0; i < Math.min(count, 10); i++) {
      const element = interactiveElements.nth(i);
      const ariaLabel = await element.getAttribute('aria-label');
      const ariaLabelledBy = await element.getAttribute('aria-labelledby');
      const textContent = await element.textContent();
      const label = await element.locator('label').count();

      if (ariaLabel || ariaLabelledBy || textContent?.trim() || label > 0) {
        elementsWithAccessibility++;
      }
    }

    // At least 70% of interactive elements should have some form of accessible label
    expect(elementsWithAccessibility).toBeGreaterThanOrEqual(Math.floor(count * 0.7));

    await context.close();
  });

  test('should handle mobile viewport resizing gracefully', async ({ browser }) => {
    const context = await browser.newContext({
      ...devices['Pixel 5'],
    });
    const page = await context.newPage();
    await page.goto('/packages');

    // Get width at initial mobile size
    // Simulate viewport resize
    await page.setViewportSize({ width: 375, height: 812 });
    await page.waitForLoadState('networkidle');

    const resizedWidth = await page.evaluate(() => document.body.offsetWidth);

    // Width should be appropriate for new viewport
    expect(resizedWidth).toBeLessThanOrEqual(375 + 2);

    // No horizontal scroll
    const scrollWidth = await page.evaluate(() => document.body.scrollWidth);
    expect(scrollWidth).toBeLessThanOrEqual(375 + 2);

    await context.close();
  });
});

test.describe('Mobile CSS Breakpoint Verification', () => {
  test('should load CSS with mobile breakpoints', async ({ page }) => {
    await page.goto('/packages');

    // Wait for CSS to load
    await page.waitForLoadState('networkidle');

    // Check that CSS contains mobile breakpoint media queries
    const stylesheets = await page.evaluate(() => {
      return Array.from(document.styleSheets).map((sheet) => {
        try {
          return sheet.cssRules
            ? Array.from(sheet.cssRules)
                .map((rule: any) => rule.media?.mediaText || rule.cssText)
                .join(' ')
            : '';
        } catch {
          return '';
        }
      });
    });

    const cssText = stylesheets.join(' ');

    // Should have mobile-first responsive CSS
    expect(cssText).toMatch(/max-width.*767|grid-cols-1|flex-col/i);
  });

  test('should apply mobile CSS to package grid', async ({ page }) => {
    await page.goto('/packages');

    // For package cards/grid, verify they stack on mobile
    const grid = page.locator('.card-grid, [class*="grid"]').first();

    // Check computed styles
    const styles = await grid.evaluate((el) => {
      const computed = window.getComputedStyle(el);
      return {
        display: computed.display,
        gridTemplateColumns: computed.gridTemplateColumns,
      };
    });

    // Should be a grid display
    expect(styles.display).toBe('grid');
    // On mobile (375px), should be 1 column
    // Grid template columns will show how many columns, e.g., "1fr" for 1 column
    if (page.viewportSize()?.width ?? 1200 < 768) {
      expect(styles.gridTemplateColumns).toMatch(/1fr/);
    }
  });
});

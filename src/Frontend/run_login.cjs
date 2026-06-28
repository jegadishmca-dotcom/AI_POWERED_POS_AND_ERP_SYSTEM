const { chromium } = require('@playwright/test');

(async () => {
  console.log("Launching browser...");
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext();
  const page = await context.newPage();

  // Log console messages
  page.on('console', msg => {
    console.log(`[BROWSER CONSOLE] ${msg.type()}: ${msg.text()}`);
  });

  // Log page errors
  page.on('pageerror', err => {
    console.log(`[BROWSER PAGE ERROR] ${err.message}`);
  });

  // Log failed requests
  page.on('requestfailed', request => {
    console.log(`[BROWSER REQUEST FAILED] ${request.url()} - ${request.failure().errorText}`);
  });
  
  page.on('response', response => {
    if (response.status() >= 400) {
      console.log(`[BROWSER HTTP ERROR] ${response.url()} - Status ${response.status()}`);
    }
  });

  try {
    console.log("Navigating to http://localhost:5173...");
    await page.goto('http://localhost:5173');
    await page.waitForTimeout(2000); // hydration wait
    
    console.log("Clicking 'ERP Back-Office' button...");
    const erpBtn = page.locator('button', { hasText: 'ERP Back-Office' }).first();
    await erpBtn.click();
    await page.waitForTimeout(500);

    console.log("Checking if submit button changed text...");
    const submitBtn = page.locator('button[type="submit"]');
    console.log("Submit button text:", await submitBtn.innerText());

    console.log("Clicking 'Quick Login as Demo Admin'...");
    const demoBtn = page.locator('button', { hasText: 'Quick Login as Demo Admin' }).first();
    await demoBtn.click();
    
    console.log("Waiting for navigation/url change...");
    for (let i = 0; i < 20; i++) {
      await page.waitForTimeout(500);
      console.log(`Current URL: ${page.url()}`);
      if (page.url().includes('dashboard') || page.url().includes('finance')) {
        console.log("SUCCESS! Navigated to dashboard.");
        break;
      }
    }
  } catch (e) {
    console.error("Test execution threw error:", e);
  } finally {
    await browser.close();
    console.log("Browser closed.");
  }
})();

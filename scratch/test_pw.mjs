import { chromium } from 'file:///C:/Users/axelm/AppData/Local/npm-cache/_npx/e41f203b7505f1fb/node_modules/playwright/index.mjs';

async function main() {
  console.log('Testing Playwright launch...');
  const browser = await chromium.launch({
    executablePath: 'C:/Users/axelm/AppData/Local/Google/Chrome/Application/chrome.exe',
    headless: true
  });
  console.log('Browser launched successfully!');
  const page = await browser.newPage();
  await page.goto('http://localhost/health');
  const text = await page.textContent('body');
  console.log('Health response from headless Chrome:', text);
  await browser.close();
}

main().catch(err => {
  console.error('Error:', err);
  process.exit(1);
});

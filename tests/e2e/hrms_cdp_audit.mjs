import fs from "node:fs/promises";

const endpoint = process.env.HRMS_CDP_ENDPOINT || "http://127.0.0.1:9333";
const baseUrl = process.env.HRMS_BASE_URL || "http://127.0.0.1:5249";
const username = process.env.HRMS_E2E_USERNAME || "";
const password = process.env.HRMS_E2E_PASSWORD;
const outputPath = process.env.HRMS_E2E_OUTPUT || "";
const maxRoutes = Number(process.env.HRMS_E2E_MAX_ROUTES || "60");
const reuseSession = process.env.HRMS_E2E_REUSE_SESSION === "1";
const authenticatedStartPath = process.env.HRMS_E2E_START_PATH || "/Admin/Index";

if ((!username || !password) && !reuseSession) {
  throw new Error("HRMS_E2E_USERNAME and HRMS_E2E_PASSWORD are required");
}

class CdpClient {
  constructor(url) {
    this.url = url;
    this.sequence = 0;
    this.pending = new Map();
    this.listeners = new Map();
  }

  async connect() {
    this.socket = new WebSocket(this.url);
    await new Promise((resolve, reject) => {
      const timer = setTimeout(() => reject(new Error("CDP connection timed out")), 10000);
      this.socket.addEventListener("open", () => {
        clearTimeout(timer);
        resolve();
      });
      this.socket.addEventListener("error", reject);
    });
    this.socket.addEventListener("message", event => {
      const message = JSON.parse(event.data);
      if (message.id) {
        const waiter = this.pending.get(message.id);
        if (!waiter) return;
        this.pending.delete(message.id);
        if (message.error) waiter.reject(new Error(message.error.message));
        else waiter.resolve(message.result || {});
        return;
      }
      const handlers = this.listeners.get(message.method) || [];
      for (const handler of handlers) handler(message.params || {});
    });
  }

  send(method, params = {}) {
    const id = ++this.sequence;
    return new Promise((resolve, reject) => {
      this.pending.set(id, { resolve, reject });
      this.socket.send(JSON.stringify({ id, method, params }));
    });
  }

  on(method, handler) {
    const handlers = this.listeners.get(method) || [];
    handlers.push(handler);
    this.listeners.set(method, handlers);
  }

  close() {
    this.socket?.close();
  }
}

async function delay(milliseconds) {
  await new Promise(resolve => setTimeout(resolve, milliseconds));
}

async function createTarget(url) {
  const response = await fetch(`${endpoint}/json/new?${encodeURIComponent(url)}`, { method: "PUT" });
  if (!response.ok) throw new Error(`Unable to create CDP target: HTTP ${response.status}`);
  return response.json();
}

const target = await createTarget(`${baseUrl}${reuseSession ? authenticatedStartPath : "/Account/Login"}`);
const cdp = new CdpClient(target.webSocketDebuggerUrl);
await cdp.connect();

const state = {
  consoleErrors: [],
  runtimeErrors: [],
  networkErrors: [],
  failedRequests: [],
  documentStatus: null,
};

cdp.on("Runtime.consoleAPICalled", event => {
  if (event.type !== "error") return;
  state.consoleErrors.push(event.args.map(item => item.value ?? item.description ?? "").join(" ").slice(0, 500));
});
cdp.on("Runtime.exceptionThrown", event => {
  state.runtimeErrors.push(String(event.exceptionDetails?.text || "Runtime exception").slice(0, 500));
});
cdp.on("Network.responseReceived", event => {
  const status = event.response.status;
  if (event.type === "Document") state.documentStatus = status;
  if (status >= 400) {
    state.networkErrors.push({ status, url: event.response.url.split("?")[0] });
  }
});
cdp.on("Network.loadingFailed", event => {
  if (event.canceled) return;
  state.failedRequests.push({ type: event.type, error: event.errorText });
});

await Promise.all([
  cdp.send("Page.enable"),
  cdp.send("Runtime.enable"),
  cdp.send("Network.enable"),
  cdp.send("Log.enable"),
]);

async function evaluate(expression, awaitPromise = true) {
  const result = await cdp.send("Runtime.evaluate", {
    expression,
    awaitPromise,
    returnByValue: true,
  });
  if (result.exceptionDetails) throw new Error(result.exceptionDetails.text || "Browser evaluation failed");
  return result.result?.value;
}

async function waitForReady(timeout = 15000) {
  const started = Date.now();
  while (Date.now() - started < timeout) {
    const ready = await evaluate("document.readyState");
    if (ready === "complete") {
      await delay(350);
      return;
    }
    await delay(100);
  }
  throw new Error("Page load timed out");
}

async function navigate(url) {
  state.documentStatus = null;
  const before = {
    console: state.consoleErrors.length,
    runtime: state.runtimeErrors.length,
    network: state.networkErrors.length,
    failed: state.failedRequests.length,
  };
  await cdp.send("Page.navigate", { url });
  await waitForReady();
  const snapshot = await evaluate(`(() => ({
    url: location.href,
    path: location.pathname,
    title: document.title,
    statusText: document.body?.innerText?.includes('An error occurred while processing your request.') ? 'generic-error' : 'ok',
    interactive: [...document.querySelectorAll('a,button,input,select,textarea,[role="button"]')]
      .filter(el => { const r=el.getBoundingClientRect(); return r.width>0 && r.height>0; })
      .map(el => ({ tag: el.tagName, id: el.id || null, name: el.getAttribute('name'), type: el.getAttribute('type'), href: el.href ? new URL(el.href).pathname : null, disabled: !!el.disabled, aria: el.getAttribute('aria-label') }))
      .slice(0, 500),
    links: [...document.querySelectorAll('a[href]')]
      .map(a => a.href)
      .filter(href => href.startsWith(${JSON.stringify(baseUrl)})),
    metrics: { width: innerWidth, scrollWidth: document.documentElement.scrollWidth, height: innerHeight, scrollHeight: document.documentElement.scrollHeight }
  }))()`);
  return {
    ...snapshot,
    httpStatus: state.documentStatus,
    consoleErrors: state.consoleErrors.slice(before.console),
    runtimeErrors: state.runtimeErrors.slice(before.runtime),
    networkErrors: state.networkErrors.slice(before.network),
    failedRequests: state.failedRequests.slice(before.failed),
  };
}

async function box(selector) {
  return evaluate(`(() => { const el=document.querySelector(${JSON.stringify(selector)}); if(!el) return null; const r=el.getBoundingClientRect(); return {x:r.left+r.width/2,y:r.top+r.height/2,width:r.width,height:r.height}; })()`);
}

async function click(selector) {
  const point = await box(selector);
  if (!point || point.width <= 0 || point.height <= 0) throw new Error(`Visible target not found: ${selector}`);
  await cdp.send("Input.dispatchMouseEvent", { type: "mousePressed", x: point.x, y: point.y, button: "left", clickCount: 1 });
  await cdp.send("Input.dispatchMouseEvent", { type: "mouseReleased", x: point.x, y: point.y, button: "left", clickCount: 1 });
}

async function type(selector, value) {
  const point = await box(selector);
  if (!point) throw new Error(`Input not found: ${selector}`);
  await click(selector);
  await evaluate(`(() => { const el=document.querySelector(${JSON.stringify(selector)}); el.value=''; el.dispatchEvent(new Event('input',{bubbles:true})); })()`);
  await cdp.send("Input.insertText", { text: value });
}

const result = {
  startedAt: new Date().toISOString(),
  browser: "Microsoft Edge via Chrome DevTools Protocol",
  baseUrl,
  login: {},
  routes: [],
  responsive: [],
  pet: {},
};

try {
  let afterLogin;
  if (reuseSession) {
    const reused = await navigate(`${baseUrl}${authenticatedStartPath}`);
    afterLogin = { url: reused.url, path: reused.path, title: reused.title, stillLogin: reused.path === "/Account/Login" };
    result.login.reusedSession = true;
  } else {
    const loginPage = await navigate(`${baseUrl}/Account/Login`);
    result.login.page = {
      httpStatus: loginPage.httpStatus,
      path: loginPage.path,
      genericError: loginPage.statusText === "generic-error",
      consoleErrors: loginPage.consoleErrors,
      networkErrors: loginPage.networkErrors,
    };

    await type('input[name="username"]', username);
    await type('input[name="password"]', password);
    const remember = await box('input[name="rememberMe"]');
    if (remember) await click('input[name="rememberMe"]');
    await click('button[type="submit"],input[type="submit"]');
    await waitForReady();
    afterLogin = await evaluate(`(() => ({ url: location.href, path: location.pathname, title: document.title, stillLogin: !!document.querySelector('input[name="password"]') }))()`);
  }
  result.login.outcome = afterLogin;
  result.login.passed = !afterLogin.stillLogin && afterLogin.path !== "/Account/Login";

  if (!result.login.passed) throw new Error("Login did not leave the login page");

  const landing = await navigate(afterLogin.url);
  const candidates = [...new Set(landing.links)]
    .map(value => new URL(value))
    .filter(url => url.origin === new URL(baseUrl).origin)
    .filter(url => !/logout|delete|remove|approve|reject|download|export|backup|restore/i.test(url.pathname))
    .map(url => `${url.origin}${url.pathname}${url.search}`)
    .slice(0, maxRoutes);

  for (const route of candidates) {
    try {
      const page = await navigate(route);
      result.routes.push({
        requestedPath: new URL(route).pathname,
        finalPath: page.path,
        httpStatus: page.httpStatus,
        genericError: page.statusText === "generic-error",
        consoleErrors: page.consoleErrors,
        runtimeErrors: page.runtimeErrors,
        networkErrors: page.networkErrors,
        failedRequests: page.failedRequests,
        interactiveCount: page.interactive.length,
        missingAccessibleNameCount: page.interactive.filter(el => ["BUTTON", "INPUT", "SELECT", "TEXTAREA"].includes(el.tag) && !el.aria && !el.name && !el.id).length,
      });
    } catch (error) {
      result.routes.push({ requestedPath: new URL(route).pathname, error: error.message });
    }
  }

  for (const viewport of [
    [1920, 1080], [1366, 768], [1024, 768], [768, 1024], [430, 932], [390, 844],
  ]) {
    await cdp.send("Emulation.setDeviceMetricsOverride", { width: viewport[0], height: viewport[1], deviceScaleFactor: 1, mobile: viewport[0] <= 430 });
    const page = await navigate(afterLogin.url);
    result.responsive.push({
      width: viewport[0],
      height: viewport[1],
      horizontalOverflow: page.metrics.scrollWidth > page.metrics.width + 2,
      scrollWidth: page.metrics.scrollWidth,
      consoleErrors: page.consoleErrors,
      networkErrors: page.networkErrors,
    });
  }

  await cdp.send("Emulation.clearDeviceMetricsOverride");
  await navigate(afterLogin.url);
  const petExists = await box("#robot-trigger");
  result.pet.triggerPresent = !!petExists;
  if (petExists) {
    await click("#robot-trigger");
    await delay(250);
    result.pet.opened = await evaluate(`(() => { const el=document.querySelector('#chat-box,#chat-container,.ai-chat-box'); if(!el) return false; const s=getComputedStyle(el); const r=el.getBoundingClientRect(); return s.display!=='none' && s.visibility!=='hidden' && r.width>0 && r.height>0; })()`);
  }
} catch (error) {
  result.fatalError = error.message;
} finally {
  result.finishedAt = new Date().toISOString();
  const serialized = JSON.stringify(result, null, 2);
  if (outputPath) await fs.writeFile(outputPath, serialized, "utf8");
  console.log(serialized);
  cdp.close();
}

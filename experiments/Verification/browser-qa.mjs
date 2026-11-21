// Local Chrome QA via the DevTools protocol. No third-party browser dependency.
import { spawn } from 'node:child_process';
import { mkdtemp, mkdir, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
let input = ''; for await (const chunk of process.stdin) input += chunk;
const { baseUrl, password, outputDirectory } = JSON.parse(input); input = '';
const profile = await mkdtemp(join(tmpdir(), 'inventory-qa-'));
const chrome = spawn(process.env.CHROME_PATH || '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome', ['--headless=new', '--remote-debugging-port=0', `--user-data-dir=${profile}`, '--no-first-run', '--no-default-browser-check', 'about:blank'], { stdio: ['ignore', 'ignore', 'pipe'] });
let stderr = ''; chrome.stderr.on('data', data => { stderr += data.toString(); });
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
async function until(fn, message, limit = 15000) { const end = Date.now() + limit; while (Date.now() < end) { const value = await fn(); if (value) return value; await sleep(100); } throw new Error(message); }
let ws; const findings = []; const runtimeErrors = [];
try {
 const match = await until(() => stderr.match(/DevTools listening on (ws:\/\/[^\s]+)/), 'Chrome debugger not available');
 const port = new URL(match[1]).port;
 const pages = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json();
 ws = new WebSocket(pages.find(p => p.type === 'page').webSocketDebuggerUrl);
 await new Promise((resolve, reject) => { ws.onopen = resolve; ws.onerror = reject; });
 let seq = 0; const pending = new Map();
 ws.onmessage = ({ data }) => { const r = JSON.parse(data); if (r.id) { const p = pending.get(r.id); if (p) { clearTimeout(p.timer); pending.delete(r.id); r.error ? p.reject(new Error(r.error.message)) : p.resolve(r.result); } } else if (r.method === 'Runtime.exceptionThrown') runtimeErrors.push(r.params.exceptionDetails.exception?.description || r.params.exceptionDetails.text); };
 function cdp(method, params = {}) { return new Promise((resolve,reject) => { const id = ++seq; const timer=setTimeout(()=>{pending.delete(id);reject(new Error('CDP timeout: '+method));},20000); pending.set(id,{resolve,reject,timer});ws.send(JSON.stringify({id,method,params})); }); }
 async function evaluate(expression) { const result=await cdp('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true}); if(result.exceptionDetails) throw new Error(result.exceptionDetails.text);return result.result.value; }
 async function navigate(path) { await cdp('Page.navigate',{url:baseUrl+path});await until(()=>evaluate('!!document.querySelector("h1")'),'Page did not render '+path).catch(async error=>{const detail=await evaluate('({path:location.pathname,text:document.body.innerText.slice(0,1800),ready:document.readyState})');throw new Error(error.message+' '+JSON.stringify(detail)+' '+runtimeErrors.join('; '));});await sleep(250); }
 await cdp('Runtime.enable'); await cdp('Page.enable');
 await cdp('Emulation.setDeviceMetricsOverride',{width:1440,height:1000,deviceScaleFactor:1,mobile:false});
 await navigate('/login').catch(async()=>{await sleep(1500);});
 async function login(role) {
  // Authenticate using the real server and persist exactly the application's session shape.
  await evaluate(`(async()=>{const r=await fetch('/api/auth/login',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({username:${JSON.stringify('verify_')}+${JSON.stringify(role.toLowerCase())},password:${JSON.stringify(password)}})});if(!r.ok)throw new Error('QA login HTTP '+r.status);const data=await r.json();sessionStorage.setItem('smart-inventory-session',JSON.stringify({accessToken:data.accessToken,expiresAt:data.expiresAt,userId:data.user.id}));sessionStorage.removeItem('smart-inventory-session-blocked');})()`);
 }
 await login('Admin');
 await mkdir(outputDirectory,{recursive:true});
 const routes=['dashboard','reports','notifications','inventory','warehouses','transfers','products','categories','stock-movements','suppliers','customers','purchase-orders','sales-orders','ai','mcp/lab','mcp/playground','mcp/trace','profile','users','settings','audit'];
 for (const mode of ['desktop-light','mobile-dark']) {
  const mobile=mode.startsWith('mobile');
  await cdp('Emulation.setDeviceMetricsOverride',{width:mobile?390:1440,height:mobile?844:1000,deviceScaleFactor:1,mobile});
  await evaluate(`localStorage.setItem('smart-inventory-appearance',${JSON.stringify(mobile?'dark':'light')})`);
  for(const route of routes) {
   await navigate('/'+route);
   const result=await evaluate(`({heading:document.querySelector('h1')?.textContent,path:location.pathname,overflow:document.documentElement.scrollWidth>innerWidth+2,theme:document.documentElement.dataset.theme,errors:[...document.querySelectorAll('[role="alert"]')].map(e=>e.textContent).filter(t=>/không.*tải|lỗi|failed/i.test(t))})`);
   if(result.overflow) throw new Error('Horizontal page overflow: '+mode+' '+route);
   if(result.path!='/'+route) throw new Error('Unexpected redirect: '+route+' -> '+result.path);
   findings.push({mode,route,...result});
   if(['dashboard','inventory','products','ai','mcp/playground'].includes(route)) {const image=await cdp('Page.captureScreenshot',{format:'png'});await writeFile(join(outputDirectory,mode+'-'+route.replaceAll('/','-')+'.png'),Buffer.from(image.data,'base64'));}
  }
 }
 const passwordFieldChecks=[];
 async function checkPasswordFields(route, count) {
  await navigate(route);
  await until(()=>evaluate(`document.querySelectorAll('input[type="password"]').length===${count}`),'Password inputs not rendered: '+route);
  const check=await evaluate(`(()=>{const inputs=[...document.querySelectorAll('input[type="password"]')];const lengths=[1,11,258];return {count:inputs.length,noLengthLimits:inputs.every(input=>input.minLength===-1&&input.maxLength===-1),acceptsLengths:lengths.every(length=>inputs.every(input=>{input.value='x'.repeat(length);return input.checkValidity()})),lengths}})()`);
  if(!check.noLengthLimits||!check.acceptsLengths)throw new Error('Password field still rejects short/long input: '+route);
  passwordFieldChecks.push({route,...check,pass:true});
 }
 await checkPasswordFields('/profile',3);
 for(const role of ['Staff','Customer']) {
  await login(role);
  for(const target of ['users','audit','inventory','mcp/lab']) {
   const expected=role==='Customer'?'/profile':(['users','audit'].includes(target)?'/dashboard':'/'+target);
   await cdp('Page.navigate',{url:baseUrl+'/'+target});
   await until(()=>evaluate(`location.pathname===${JSON.stringify(expected)} && !!document.querySelector('h1')`),'Role route failure '+role+' '+target);
   findings.push({role,route:target,actualPath:expected,pass:true});
  }
 }
 await evaluate("sessionStorage.clear();sessionStorage.setItem('smart-inventory-session-blocked','1')");
 await checkPasswordFields('/register',2);
 const publicAuthChecks=[];
 await evaluate("document.querySelector('form').requestSubmit()");
 await until(()=>evaluate("document.querySelectorAll('input[aria-invalid=\"true\"]').length===5"),'Registration must show all five required-field errors');
 publicAuthChecks.push({name:'Registration requires all five fields',pass:true});
 await navigate('/login');
 const forgotLink=await evaluate("!!document.querySelector('a[href=\"/forgot-password\"]')");
 if(!forgotLink)throw new Error('Forgot password link missing from login');
 publicAuthChecks.push({name:'Login links to password recovery',pass:true});
 await navigate('/forgot-password');
 const requiredRecovery=await evaluate("[...document.querySelectorAll('input')].filter(input=>input.required).length===2");
 if(!requiredRecovery)throw new Error('Recovery identity fields must be required');
 publicAuthChecks.push({name:'Recovery requires username and email',pass:true});
 await navigate('/reset-password');
 const invalidLink=await evaluate("!!document.querySelector('[role=\"alert\"]') && document.querySelectorAll('input[type=\"password\"]').length===0");
 if(!invalidLink)throw new Error('Reset page must reject a missing token');
 publicAuthChecks.push({name:'Reset page rejects a missing credential',pass:true});
 await writeFile(join(outputDirectory,'public-auth-results.json'),JSON.stringify({passed:true,checks:publicAuthChecks},null,2));
 await writeFile(join(outputDirectory,'password-field-results.json'),JSON.stringify({passed:true,checks:passwordFieldChecks},null,2));
 if(runtimeErrors.length) throw new Error('Browser runtime exceptions: '+runtimeErrors.join('; '));
 await writeFile(join(outputDirectory,'browser-results.json'),JSON.stringify({passed:true,findings,runtimeErrors},null,2));
 console.log('Browser QA passed: '+findings.length+' route/viewport checks.');
} finally {
 ws?.close(); chrome.kill(); await sleep(300); await rm(profile,{recursive:true,force:true,maxRetries:3,retryDelay:300});
}

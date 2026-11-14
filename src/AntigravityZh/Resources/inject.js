// Injected into the Antigravity hub renderer page.
// Scope: UI chrome only. Never touch conversation markdown, code, terminal, inputs.
(() => {
  if (window.__agyZhInstalled) {
    window.__agyZhUpdate && window.__agyZhUpdate();
    return;
  }
  window.__agyZhInstalled = true;
  window.__agyZhEnabled = true;

  const SKIP_SELECTOR = [
    '[data-markdown-root]',
    'pre',
    'code',
    'kbd',
    'textarea',
    'input',
    'select',
    '.xterm',
    '.monaco-editor',
    'script',
    'style',
    'noscript',
    'svg',
    '[contenteditable="true"]',
    '[data-agy-zh-skip]',
  ].join(',');

  const ATTRS = ['placeholder', 'title', 'aria-label', 'alt'];

  let dict = {};
  const patterns = [
    [/^Worked for (\d+)m$/, '工作了 $1 分钟'],
    [/^Worked for (\d+)s$/, '工作了 $1 秒'],
    [/^Worked for (\d+)h$/, '工作了 $1 小时'],
    [/^Thought for (\d+)m$/, '思考了 $1 分钟'],
    [/^Thought for (\d+)s$/, '思考了 $1 秒'],
    [/^Worked for (.+)$/, '工作了 $1'],
    [/^Working for (.+)$/, '正在工作 $1'],
    [/^Thought for (.+)$/, '思考了 $1'],
    [/^Thinking for (.+)$/, '思考中 $1'],
    [/^Ran for (.+)$/, '运行了 $1'],
    [/^Waited for (.+)$/, '等待了 $1'],
    [/^(\d+)\s*(?:m|min|mins|minutes?) ago$/, '$1 分钟前'],
    [/^(\d+)\s*(?:h|hr|hrs|hours?) ago$/, '$1 小时前'],
    [/^(\d+)\s*(?:s|sec|secs|seconds?) ago$/, '$1 秒前'],
    [/^(\d+)\s*d ago$/, '$1 天前'],
    [/^(\d+)m$/, '$1 分钟'],
    [/^(\d+)h$/, '$1 小时'],
    [/^(\d+)s$/, '$1 秒'],
    [/^(\d+)d$/, '$1 天'],
    [/^Just now$/, '刚刚'],
    [/^Just now\.$/, '刚刚。'],
    [/^Yesterday (.+)$/, '昨天 $1'],
    [/^Yesterday$/, '昨天'],
    [/^Today (.+)$/, '今天 $1'],
    [/^(\d+)\s*files? changed$/, '$1 个文件已更改'],
    [/^(\d+)\s*files?$/, '$1 个文件'],
    [/^(\d+)\s*changes?$/, '$1 处更改'],
    [/^(\d+)\s*questions?$/, '$1 个问题'],
    [/^(\d+)\s*answers?$/, '$1 个回答'],
    [/^(\d+)\s*attachments?$/, '$1 个附件'],
    [/^(\d+)\s*results?$/, '$1 个结果'],
    [/^(\d+)\s*items?$/, '$1 项'],
    [/^Show (\d+) breakdowns?$/, '显示 $1 项明细'],
    [/^Your Plan: (.+)$/, '你的方案：$1'],
    [/^Reading (.+)$/, '正在读取 $1'],
    [/^Requesting permission to read (.+)$/, '请求读取 $1 的权限'],
    [/^Send feedback as (.+)$/, '以 $1 身份发送反馈'],
    [/^Analyzing (.+)$/, '正在分析 $1'],
    [/^Listing (.+)$/, '正在列出 $1'],
    [/^Listed (.+)$/, '已列出 $1'],
    [/^Created (.+)$/, '已创建 $1'],
    [/^Edited (.+)$/, '已编辑 $1'],
    [/^Ran (.+)$/, '已运行 $1'],
    [/^Read (.+)$/, '已读取 $1'],
    [/^Wrote (.+)$/, '已写入 $1'],
    [/^Deleted (.+)$/, '已删除 $1'],
    [/^Moved (.+)$/, '已移动 $1'],
    [/^Copied (.+)$/, '已复制 $1'],
    [/^Searched (.+)$/, '已搜索 $1'],
    [/^Fetched (.+)$/, '已获取 $1'],
    [/^Listed$/, '已列出'],
    [/^(\d+) tools?$/, '$1 个工具'],
    [/^Hey (.+), what should we build today\?$/, '嗨，$1，今天想构建什么？'],
    [/^Good to see you, (.+?)\. What's next\?$/, '很高兴见到你，$1。接下来做什么？'],
    [/^Welcome back, (.+?)\. Where should we pick up\?$/, '欢迎回来，$1。从哪里继续？'],
    [/^Welcome back, (.+)$/, '欢迎回来，$1'],
    [/^(.+?)\. Where should we pick up\?$/, '$1。从哪里继续？'],
    [/^(.+?)\. What's next\?$/, '$1。接下来做什么？'],
    [/^See (\d+) more$/, '再看 $1 个'],
    [/^Resets in (.+)$/, '$1 后重置'],
    [/^Running (.+)$/, '正在运行 $1'],
    [/^Loading (.+)\.\.\.$/, '正在加载 $1…'],
    [/^in (\d+) days?, (\d+) hours?\.$/, '$1 天 $2 小时后'],
    [/^in (\d+) hours?, (\d+) minutes?\.$/, '$1 小时 $2 分钟后'],
    [/^(\d+)\s*of\s*(\d+)$/, '$1 / $2'],
  ];

  function collapse(s) {
    return s.replace(/\s+/g, ' ').trim();
  }

  function translate(s) {
    let hit = dict[s];
    if (hit !== undefined) return hit;
    const c = collapse(s);
    if (c !== s) {
      hit = dict[c];
      if (hit !== undefined) return hit;
    }
    for (const [re, rep] of patterns) {
      if (re.test(c)) return c.replace(re, rep);
    }
    // Preserve trailing punctuation that the dictionary key lacks.
    const m = c.match(/^(.*?)([?:!….:]+)$/);
    if (m) {
      const base = dict[m[1]];
      if (base !== undefined) return base + m[2];
    }
    const lead = s.match(/^\s*/)[0];
    return null;
  }

  // Short words that often start a longer sentence split across several text nodes
  // (e.g. "No" + " automations yet"). Only translate them when they stand alone.
  const RISKY_SINGLE = new Set([
    'No', 'Yes', 'New', 'All', 'Set', 'Fast', 'Notice', 'Auto', 'More', 'File', 'Files',
    'Name', 'Type', 'Size', 'Status', 'Date', 'Text', 'Error', 'Help', 'Open', 'Close',
    'Back', 'Next', 'Add', 'Copy', 'Edit', 'Search', 'Show', 'Hide', 'Save', 'Stop',
    'Start', 'Run', 'Delete', 'Remove', 'Cancel', 'Settings', 'Project', 'Projects',
    'Model', 'Models', 'Agent', 'Agents', 'Browser', 'Terminal', 'General', 'Software',
    'Account', 'Home', 'End', 'Down', 'Left', 'Right', 'On', 'Off', 'OK', 'Ok', 'Done',
  ]);

  function inSkipped(node) {
    let el = node.nodeType === 3 ? node.parentElement : node;
    while (el) {
      if (el.matches && el.matches(SKIP_SELECTOR)) return true;
      el = el.parentElement;
    }
    return false;
  }

  function isStandaloneWord(node, word) {
    const el = node.parentElement;
    if (!el) return false;
    try { return collapse(el.innerText || '') === word; } catch { return false; }
  }

  // Some React strings are split into several sibling text nodes, so exact-match
  // on a single node misses them. If an element contains only text and its full
  // text matches a dictionary key, put the translation in the first node and
  // blank the rest (keeps React's node references alive).
  function tryCompose(el) {
    if (!window.__agyZhEnabled) return false;
    if (!el || el.nodeType !== 1) return false;
    if (!el.childNodes || el.childNodes.length < 2 || el.childNodes.length > 8) return false;
    if (el.children && el.children.length > 0) return false;
    if (inSkipped(el)) return false;
    const texts = [];
    for (const n of el.childNodes) {
      if (n.nodeType === 3) texts.push(n);
      else if (n.nodeType !== 8) return false;
    }
    if (texts.length < 2) return false;
    const raw = texts.map((n) => n.nodeValue).join('');
    const trimmed = collapse(raw);
    if (!trimmed || trimmed.length > 220 || !/[A-Za-z]/.test(trimmed)) return false;
    const t = translate(trimmed);
    if (t === null) return false;
    const lead = raw.match(/^\s*/)[0];
    const tail = raw.match(/\s*$/)[0];
    texts[0].nodeValue = lead + t + tail;
    for (let i = 1; i < texts.length; i++) texts[i].nodeValue = '';
    return true;
  }

  function doText(node) {
    if (!window.__agyZhEnabled) return;
    const raw = node.nodeValue;
    if (!raw) return;
    const trimmed = raw.trim();
    if (!trimmed || trimmed.length > 300) return;
    if (!/[A-Za-z]/.test(trimmed)) return;
    if (!trimmed.includes(' ') && RISKY_SINGLE.has(trimmed) && !isStandaloneWord(node, trimmed)) return;
    if (inSkipped(node)) return;
    const t = translate(trimmed);
    if (t === null) return;
    const lead = raw.match(/^\s*/)[0];
    const tail = raw.match(/\s*$/)[0];
    const next = lead + t + tail;
    if (next !== raw) node.nodeValue = next;
  }

  function inSkippedForAttrs(el) {
    let p = el.parentElement;
    while (p) {
      if (p.matches && p.matches(SKIP_SELECTOR)) return true;
      p = p.parentElement;
    }
    return false;
  }

  function doAttrs(el) {
    if (!window.__agyZhEnabled) return;
    if (!el.hasAttributes || inSkippedForAttrs(el)) return;
    for (const a of ATTRS) {
      if (!el.hasAttribute(a)) continue;
      const v = el.getAttribute(a);
      if (!v) continue;
      const t = translate(v);
      if (t !== null && t !== v) {
        const lead = v.match(/^\s*/)[0];
        const tail = v.match(/\s*$/)[0];
        el.setAttribute(a, lead + t + tail);
      }
    }
  }

  function walk(root) {
    if (!root) return;
    if (root.nodeType === 3) return doText(root);
    if (root.nodeType === 1) {
      tryCompose(root);
      doAttrs(root);
    }
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT | NodeFilter.SHOW_ELEMENT);
    let n;
    while ((n = walker.nextNode())) {
      if (n.nodeType === 3) doText(n);
      else {
        tryCompose(n);
        doAttrs(n);
      }
    }
  }

  let scheduled = false;
  let pending = [];
  function schedule(mutations) {
    pending.push(...mutations);
    if (scheduled) return;
    scheduled = true;
    setTimeout(() => {
      scheduled = false;
      const batch = pending;
      pending = [];
      for (const m of batch) {
        if (m.type === 'characterData') {
          doText(m.target);
          if (m.target.parentElement) tryCompose(m.target.parentElement);
        }
        else if (m.type === 'attributes') doAttrs(m.target);
        else {
          for (const added of m.addedNodes) walk(added);
        }
      }
    }, 30);
  }

  const observer = new MutationObserver((mutations) => {
    schedule(mutations);
  });

  function translateTitle() {
    const t = document.title;
    if (t && t.includes(' - Outside of Project - ')) {
      document.title = t.replace(' - Outside of Project - ', ' - 项目外 - ');
    }
  }

  window.__agyZhUpdate = () => { translateTitle(); walk(document.body); };
  window.__agyZhSetDict = (d) => { dict = d || {}; window.__agyZhUpdate(); };
  window.__agyZhToggle = () => { window.__agyZhEnabled = !window.__agyZhEnabled; };

  // Used by the helper to log strings that are still untranslated.
  window.__agyZhMissing = (limit = 200) => {
    const out = [];
    const seen = new Set();
    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
    let n;
    while ((n = walker.nextNode()) && out.length < limit) {
      const s = (n.nodeValue || '').trim();
      if (!s || s.length < 2 || s.length > 80) continue;
      if (!/[A-Za-z]/.test(s) || /[\u4e00-\u9fff]/.test(s)) continue;
      if (inSkipped(n)) continue;
      const c = collapse(s);
      if (dict[c] !== undefined) continue;
      if (patterns.some(([re]) => re.test(c))) continue;
      if (seen.has(c)) continue;
      seen.add(c);
      out.push(c);
    }
    return out;
  };

  const start = () => {
    translateTitle();
    walk(document.body);
    [500, 1500, 4000].forEach((ms) => setTimeout(() => window.__agyZhUpdate && window.__agyZhUpdate(), ms));
    observer.observe(document.documentElement, {
      childList: true,
      subtree: true,
      characterData: true,
      attributes: true,
      attributeFilter: ATTRS,
    });
  };
  if (document.body) start();
  else document.addEventListener('DOMContentLoaded', start, { once: true });
})();

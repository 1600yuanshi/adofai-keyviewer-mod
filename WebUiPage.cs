namespace ADOFAI.AgentKeyViewer
{
    /// <summary>
    /// 内嵌的 WebUI 单页界面（HTML + 原生 JS，无外部依赖）。
    /// 注意：为了在 C# 逐字字符串中免去大量双引号转义，本页面内统一使用单引号书写
    /// HTML 属性与 JS 字符串，请勿改成双引号。
    /// </summary>
    public static class WebUiPage
    {
        public const string Html = @"<!DOCTYPE html>
<html lang='zh-CN'>
<head>
<meta charset='utf-8'>
<meta name='viewport' content='width=device-width,initial-scale=1'>
<title>Agent Key Viewer · 配置生成器</title>
<style>
:root{--bg:#0e1116;--panel:#161b22;--panel2:#1c2230;--line:#2a3240;--fg:#e6edf3;--dim:#8b98a8;
--acc:#4aa8ff;--acc2:#b478ff;--ok:#3fb950;--err:#f85149;--warn:#d29922}
*{box-sizing:border-box}
body{margin:0;background:var(--bg);color:var(--fg);font:14px/1.6 'Microsoft YaHei',system-ui,sans-serif}
header{padding:16px 22px;border-bottom:1px solid var(--line);display:flex;align-items:center;gap:14px;flex-wrap:wrap}
h1{font-size:17px;margin:0;font-weight:600}
h1 span{color:var(--acc)}
.badge{font-size:12px;color:var(--dim);border:1px solid var(--line);border-radius:20px;padding:2px 10px}
.badge.on{color:var(--ok);border-color:#1d4326}
main{max-width:1080px;margin:0 auto;padding:20px 22px 60px}
nav{display:flex;gap:6px;margin-bottom:18px;flex-wrap:wrap}
nav button{background:var(--panel);border:1px solid var(--line);color:var(--dim);padding:8px 16px;border-radius:8px;cursor:pointer;font-size:14px}
nav button.active{background:var(--acc);border-color:var(--acc);color:#08121e;font-weight:600}
section{display:none}
section.active{display:block}
.card{background:var(--panel);border:1px solid var(--line);border-radius:12px;padding:18px;margin-bottom:16px}
.card h2{font-size:14px;margin:0 0 12px;color:var(--dim);font-weight:600;letter-spacing:.5px}
label{display:block;font-size:13px;color:var(--dim);margin:12px 0 6px}
textarea,input,select{width:100%;background:var(--bg);border:1px solid var(--line);color:var(--fg);
border-radius:8px;padding:10px 12px;font:13px/1.6 inherit;outline:none}
textarea:focus,input:focus,select:focus{border-color:var(--acc)}
textarea{min-height:96px;resize:vertical}
.row{display:flex;gap:12px;flex-wrap:wrap}
.row>div{flex:1;min-width:180px}
button.primary{background:var(--acc);border:none;color:#08121e;font-weight:600;padding:11px 26px;border-radius:8px;cursor:pointer;font-size:14px}
button.primary:disabled{opacity:.5;cursor:not-allowed}
button.ghost{background:transparent;border:1px solid var(--line);color:var(--fg);padding:8px 14px;border-radius:8px;cursor:pointer}
button.ghost:hover{border-color:var(--acc)}
button.danger{border-color:#5a2626;color:var(--err)}
.actions{display:flex;gap:10px;align-items:center;margin-top:16px;flex-wrap:wrap}
.hint{font-size:12px;color:var(--dim);margin-top:8px}
.msg{margin-top:14px;padding:10px 14px;border-radius:8px;font-size:13px;display:none;white-space:pre-wrap}
.msg.show{display:block}
.msg.ok{background:#10251a;color:var(--ok);border:1px solid #1d4326}
.msg.err{background:#2a1416;color:var(--err);border:1px solid #5a2626}
.msg.busy{background:#1a2333;color:var(--acc);border:1px solid #23405e}
pre{background:#0a0d12;border:1px solid var(--line);border-radius:8px;padding:12px;overflow:auto;
max-height:420px;font:12px/1.55 Consolas,monospace;white-space:pre-wrap;word-break:break-all;margin:12px 0 0}
table{width:100%;border-collapse:collapse;font-size:13px}
th,td{text-align:left;padding:9px 10px;border-bottom:1px solid var(--line)}
th{color:var(--dim);font-weight:600;font-size:12px}
td.mono{font-family:Consolas,monospace;font-size:12px;color:var(--dim)}
td button{margin-right:6px}
.tip{background:var(--panel2);border-left:3px solid var(--acc2);padding:10px 14px;border-radius:6px;font-size:13px;color:var(--dim);margin-bottom:14px}
</style>
</head>
<body>
<header>
  <h1>Agent Key Viewer <span>· CherryTools 配置生成器</span></h1>
  <div class='badge' id='modBadge'>模组状态…</div>
  <div class='badge' id='portBadge'></div>
</header>
<main>
  <nav>
    <button data-tab='kv' class='active'>生成 .ctkv</button>
    <button data-tab='ov'>生成 OV 覆盖物</button>
    <button data-tab='keys'>密钥管理</button>
    <button data-tab='prompts'>提示词</button>
    <button data-tab='update'>更新</button>
    <button data-tab='files'>文件管理</button>
  </nav>

  <section id='tab-kv' class='active'>
    <div class='card'>
      <h2>用一句话描述你想要的按键显示效果</h2>
      <div class='tip'>例：16K 全键盘，紫色到天蓝渐变边框，按下白色高亮，开启键雨，KPS 放最左、Total 放最右</div>
      <textarea id='kvPrompt' placeholder='在此输入需求描述…'></textarea>
      <div class='row'>
        <div>
          <label>生成模式</label>
          <select id='kvMode'>
            <option value='0'>AI 输出 JSON 规格（代码构建，推荐）</option>
            <option value='1'>AI 直写 XML</option>
          </select>
        </div>
        <div>
          <label>目标格式</label>
          <select id='kvFormat'>
            <option value='0'>Sonnet 新格式（CT 26.5+）</option>
            <option value='1'>旧格式（旧版 CT）</option>
          </select>
        </div>
        <div>
          <label>使用密钥</label>
          <select id='kvKey'></select>
        </div>
      </div>
      <div class='actions'>
        <button class='primary' id='kvGen'>生成配置</button>
        <span class='hint' id='kvStatus'></span>
      </div>
      <div class='msg' id='kvMsg'></div>
    </div>
    <div class='card' id='kvResultCard' style='display:none'>
      <h2>生成结果</h2>
      <div class='actions'>
        <input id='kvFileName' placeholder='文件名（留空则用配置名）' style='max-width:280px'>
        <button class='primary' id='kvSave'>保存到 ctkv 目录</button>
      </div>
      <pre id='kvPreview'></pre>
    </div>
  </section>

  <section id='tab-ov'>
    <div class='card'>
      <h2>描述你想要的 Overlayer 覆盖物</h2>
      <div class='tip'>例：左上角显示 KPS 和实时 BPM，右下角显示准确率，底部一条进度条，白色描边黑字</div>
      <textarea id='ovPrompt' placeholder='在此输入需求描述…'></textarea>
      <div class='row'>
        <div>
          <label>使用密钥</label>
          <select id='ovKey'></select>
        </div>
      </div>
      <div class='actions'>
        <button class='primary' id='ovGen'>生成 OV 配置</button>
        <span class='hint' id='ovStatus'></span>
      </div>
      <div class='msg' id='ovMsg'></div>
    </div>
    <div class='card' id='ovResultCard' style='display:none'>
      <h2>生成结果</h2>
      <div class='actions'>
        <input id='ovFileName' placeholder='文件名（留空自动命名）' style='max-width:280px'>
        <button class='primary' id='ovSave'>保存为 .ctov</button>
        <button class='ghost' id='ovSaveSettings'>导出为 CT 设置文件</button>
      </div>
      <div class='tip' style='margin-top:12px'>
        <b>当前 CT 的「导入 .ctov」有 Bug</b>（Overlayer 模块读取时用到 <span class='mono'>XmlReaderSettings.DtdProcessing</span>，在当前 Unity 运行时无法解析，CT 自己导出的包也一样导入失败）。<br>
        因此推荐用「<b>导出为 CT 设置文件</b>」绕过。导出时会<b>读取 CT 现有设置并追加合并</b>（同名组件自动改名），不会清掉你已有的覆盖物。<br>
        步骤：① 点「导出为 CT 设置文件」；② <b>完全关闭游戏</b>（CT 会在关闭时回写设置，必须在关闭后替换）；<br>
        ③ 把导出的文件复制并改名覆盖到：<br>
        <span class='mono' id='ovTargetPath'></span><br>
        ④ 重新启动游戏即可看到覆盖物。
      </div>
      <pre id='ovPreview'></pre>
    </div>
  </section>

  <section id='tab-keys'>
    <div class='card'>
      <h2>已保存的密钥</h2>
      <table><thead><tr><th>名称</th><th>供应商</th><th>模型</th><th>Key</th><th>操作</th></tr></thead>
      <tbody id='keyRows'></tbody></table>
      <div class='hint'>密钥保存在游戏目录 AgentKeyViewer_config/apikeys.json，重启不丢失。</div>
    </div>
    <div class='card'>
      <h2>添加 / 更新密钥</h2>
      <div class='row'>
        <div>
          <label>供应商预设</label>
          <select id='kProvider'></select>
        </div>
        <div>
          <label>模型</label>
          <select id='kModel'></select>
        </div>
      </div>
      <label>名称</label><input id='kName' placeholder='例如 DeepSeek 主号'>
      <label>Base URL</label><input id='kBase' placeholder='https://api.deepseek.com'>
      <label>API Key</label><input id='kKey' placeholder='sk-…' type='password'>
      <div class='actions'>
        <button class='primary' id='kAdd'>保存密钥</button>
        <span class='hint'>留空 API Key 表示只更新名称/地址/模型</span>
      </div>
      <div class='msg' id='kMsg'></div>
    </div>
    <div class='card'>
      <h2>从 agent 配置导入</h2>
      <label>agent 配置文件路径（根目录的 agent_config.txt）</label>
      <input id='kAgentPath' placeholder='例如 D:\...\agent_config.txt'>
      <div class='actions'>
        <button class='ghost' id='kImportAgent'>导入其中的 API_KEY</button>
      </div>
      <div class='msg' id='kAgentMsg'></div>
    </div>
  </section>

  <section id='tab-prompts'>
    <div class='card'>
      <h2>提示词（改完保存即生效，不需要重启游戏）</h2>
      <div class='tip'>提示词已外置为磁盘文件，可在此直接编辑保存，也可用任意编辑器修改 <span class='mono' id='pDir'></span> 下的 .txt 后点「重载」。保存/重载后<b>下一次生成</b>即使用新内容。</div>
      <div class='actions'>
        <button class='ghost' id='pReloadAll'>从磁盘重载全部</button>
        <span class='hint' id='pStatus'></span>
      </div>
      <table><thead><tr><th>用途</th><th>键名</th><th>来源</th><th>长度</th></tr></thead>
      <tbody id='pRows'></tbody></table>
    </div>
    <div class='card' id='pEditCard' style='display:none'>
      <h2 id='pEditTitle'>编辑提示词</h2>
      <textarea id='pText' style='min-height:340px' placeholder='提示词内容…'></textarea>
      <div class='actions'>
        <button class='primary' id='pSave'>保存并生效</button>
        <button class='ghost' id='pReset'>恢复内置默认</button>
        <span class='hint' id='pEditHint'></span>
      </div>
      <div class='msg' id='pMsg'></div>
    </div>
    <div class='card'>
      <h2>供应商预设</h2>
      <div class='tip'>JSON 数组，每项含 name / baseUrl / model / models / thinkingDisableJson / maxTokens。保存后立即生效。</div>
      <div class='actions'>
        <button class='ghost' id='prLoad'>重新读取</button>
        <button class='ghost' id='prReset'>恢复内置默认</button>
        <span class='hint' id='prPath'></span>
      </div>
      <textarea id='prText' style='min-height:240px' placeholder='[ … ]'></textarea>
      <div class='actions'>
        <button class='primary' id='prSave'>保存预设</button>
      </div>
      <div class='msg' id='prMsg'></div>
    </div>
  </section>

  <section id='tab-update'>
    <div class='card'>
      <h2>版本与热更新</h2>
      <div class='tip'>
        模组拆成两部分：<b>引导器</b>（UMM 入口，需重启游戏才能换）与 <b>核心</b>（全部功能，可热替换）。<br>
        更新只改核心时，<b>不需要重启游戏</b>；只有引导器也变了才需要重启。
      </div>
      <table><tbody>
        <tr><th>当前版本</th><td class='mono' id='uCur'></td></tr>
        <tr><th>最新版本</th><td class='mono' id='uLatest'></td></tr>
        <tr><th>状态</th><td class='mono' id='uState'></td></tr>
        <tr><th>已热更新</th><td class='mono' id='uCount'></td></tr>
      </tbody></table>
      <div class='actions'>
        <button class='primary' id='uCheck'>检查更新</button>
        <button class='ghost' id='uDownload'>下载更新</button>
        <button class='primary' id='uApply'>应用并热生效</button>
      </div>
      <div class='msg' id='uMsg'></div>
      <pre id='uNotes' style='display:none'></pre>
    </div>
    <div class='card'>
      <h2>手动热重载核心</h2>
      <div class='tip'>本地改完代码重新编译后，点一下即可让新核心生效，<b>不用重启游戏</b>。重载会重启 WebUI 服务并更换访问令牌，请回到 UMM 面板取新地址。</div>
      <div class='actions'>
        <button class='ghost' id='uReload'>立即热重载核心</button>
      </div>
      <div class='msg' id='uReloadMsg'></div>
    </div>
  </section>

  <section id='tab-files'>
    <div class='card'>
      <h2>配置包文件</h2>
      <div class='actions'>
        <button class='ghost' id='fRefresh'>刷新列表</button>
        <button class='ghost' id='fOpen'>打开目录</button>
      </div>
      <table><thead><tr><th>文件名</th><th>类型</th><th>大小</th><th>修改时间</th><th>操作</th></tr></thead>
      <tbody id='fileRows'></tbody></table>
      <div class='msg' id='fMsg'></div>
    </div>
    <div class='card' id='fPreviewCard' style='display:none'>
      <h2 id='fPreviewTitle'>内容预览</h2>
      <pre id='fPreview'></pre>
    </div>
  </section>
</main>

<script>
var TOKEN = new URLSearchParams(location.search).get('t') || '';
var $ = function(id){ return document.getElementById(id); };

function api(path, opt){
  opt = opt || {};
  var url = path + (path.indexOf('?') >= 0 ? '&' : '?') + 't=' + encodeURIComponent(TOKEN);
  var init = { headers: { 'X-Token': TOKEN } };
  if (opt.body !== undefined){
    init.method = 'POST';
    init.headers['Content-Type'] = 'application/json';
    init.body = JSON.stringify(opt.body);
  }
  return fetch(url, init).then(function(r){ return r.json(); });
}

function showMsg(el, text, kind){
  var e = $(el);
  e.textContent = text;
  e.className = 'msg show ' + (kind || 'ok');
}
function hideMsg(el){ $(el).className = 'msg'; }

// ---------- 选项卡 ----------
document.querySelectorAll('nav button').forEach(function(b){
  b.onclick = function(){
    document.querySelectorAll('nav button').forEach(function(x){ x.classList.remove('active'); });
    document.querySelectorAll('section').forEach(function(x){ x.classList.remove('active'); });
    b.classList.add('active');
    $('tab-' + b.dataset.tab).classList.add('active');
    if (b.dataset.tab === 'files') loadFiles();
    if (b.dataset.tab === 'prompts') loadPrompts();
    if (b.dataset.tab === 'update') loadUpdate();
  };
});

// ---------- 状态轮询 ----------
var lastJob = null;
function refreshState(){
  api('/api/state').then(function(s){
    if (!s.ok) return;
    $('modBadge').textContent = s.modEnabled ? '模组已启用' : '模组未启用';
    $('modBadge').className = 'badge ' + (s.modEnabled ? 'on' : '');
    $('portBadge').textContent = '端口 ' + s.port;

    var g = s.gen || {};
    if (s.ctOvSettingsPath) $('ovTargetPath').textContent = s.ctOvSettingsPath;
    var st = g.busy ? '生成中…' : (g.state === 'Success' ? '已完成' : (g.state === 'Failed' ? '失败' : '空闲'));
    $('kvStatus').textContent = st;
    $('ovStatus').textContent = st;

    if (g.busy){
      showMsg(g.job === 'Ov' ? 'ovMsg' : 'kvMsg', 'AI 正在生成，请稍候…', 'busy');
      $('kvGen').disabled = true; $('ovGen').disabled = true;
    } else {
      $('kvGen').disabled = false; $('ovGen').disabled = false;
      if (g.state === 'Failed' && g.error) showMsg(g.job === 'Ov' ? 'ovMsg' : 'kvMsg', g.error, 'err');
      if (g.state === 'Success' && lastJob !== g.state + (g.savedCtkv || '') + (g.savedOv || '')){
        if (g.rawPreview){
          if (g.job === 'Ov'){ $('ovResultCard').style.display = 'block'; $('ovPreview').textContent = g.rawPreview; }
          else { $('kvResultCard').style.display = 'block'; $('kvPreview').textContent = g.rawPreview; }
        }
        if (g.savedCtkv) showMsg('kvMsg', '已保存：' + g.savedCtkv, 'ok');
        if (g.savedOv) showMsg('ovMsg', '已保存：' + g.savedOv, 'ok');
      }
    }

    // 设置回填（仅首次）
    if (!refreshState.inited){
      refreshState.inited = true;
      $('kvMode').value = String((s.settings || {}).genMode || 0);
      $('kvFormat').value = String((s.settings || {}).targetFormat || 0);
      fillProviders(s.providers || []);
      fillKeys(s.keys || [], s.selectedKeyIndex);
    }
    lastJob = g.state + (g.savedCtkv || '') + (g.savedOv || '');
  }).catch(function(){});
}
setInterval(refreshState, 1200);

function fillKeys(keys, sel){
  ['kvKey','ovKey'].forEach(function(id){
    var el = $(id);
    el.innerHTML = '';
    keys.forEach(function(k){
      var o = document.createElement('option');
      o.value = String(k.index);
      o.textContent = k.name + '（' + (k.model || '未指定模型') + '）';
      el.appendChild(o);
    });
    if (sel >= 0 && sel < keys.length) el.value = String(sel);
  });
  renderKeyRows(keys);
}

function renderKeyRows(keys){
  var tb = $('keyRows');
  tb.innerHTML = '';
  if (!keys.length){
    tb.innerHTML = '<tr><td colspan=5 class=mono>还没有密钥，请在下方添加</td></tr>';
    return;
  }
  keys.forEach(function(k){
    var tr = document.createElement('tr');
    tr.innerHTML = '<td>' + esc(k.name) + (k.isDefault ? ' <span style=color:var(--acc)>·默认</span>' : '') + '</td>'
      + '<td class=mono>' + esc(k.baseUrl) + '</td>'
      + '<td class=mono>' + esc(k.model) + '</td>'
      + '<td class=mono>' + esc(k.masked) + '</td>';
    var td = document.createElement('td');
    var b1 = document.createElement('button');
    b1.className = 'ghost'; b1.textContent = '设为默认';
    b1.onclick = function(){ api('/api/keys', { body: { action: 'default', index: k.index } }).then(refreshState); };
    var b2 = document.createElement('button');
    b2.className = 'ghost danger'; b2.textContent = '删除';
    b2.onclick = function(){
      if (!confirm('确定删除密钥「' + k.name + '」？')) return;
      api('/api/keys', { body: { action: 'delete', index: k.index } }).then(refreshState);
    };
    td.appendChild(b1); td.appendChild(b2);
    tr.appendChild(td);
    tb.appendChild(tr);
  });
}

function esc(s){
  return String(s == null ? '' : s).replace(/[&<>]/g, function(c){
    return c === '&' ? '&amp;' : (c === '<' ? '&lt;' : '&gt;');
  });
}

// ---------- 供应商 ----------
var PROVIDERS = [];
function fillProviders(list){
  PROVIDERS = list;
  var sel = $('kProvider');
  sel.innerHTML = '';
  list.forEach(function(p, i){
    var o = document.createElement('option');
    o.value = String(i); o.textContent = p.name;
    sel.appendChild(o);
  });
  sel.onchange = applyProvider;
  applyProvider();
}
function applyProvider(){
  var p = PROVIDERS[parseInt($('kProvider').value, 10) || 0];
  if (!p) return;
  $('kBase').value = p.baseUrl || '';
  var ms = $('kModel');
  ms.innerHTML = '';
  var models = (p.models && p.models.length) ? p.models : (p.model ? [p.model] : []);
  models.forEach(function(m){
    var o = document.createElement('option'); o.value = m; o.textContent = m;
    ms.appendChild(o);
  });
  if (!models.length){
    var o = document.createElement('option'); o.value = ''; o.textContent = '（自行填写）';
    ms.appendChild(o);
  }
}

// ---------- 生成 ----------
function startGen(job){
  var promptEl = job === 'ov' ? $('ovPrompt') : $('kvPrompt');
  var prompt = promptEl.value.trim();
  if (!prompt){ showMsg(job === 'ov' ? 'ovMsg' : 'kvMsg', '请先填写需求描述', 'err'); return; }
  var body = {
    prompt: prompt,
    job: job,
    keyIndex: parseInt((job === 'ov' ? $('ovKey') : $('kvKey')).value, 10),
    mode: parseInt($('kvMode').value, 10),
    format: parseInt($('kvFormat').value, 10)
  };
  hideMsg(job === 'ov' ? 'ovMsg' : 'kvMsg');
  showMsg(job === 'ov' ? 'ovMsg' : 'kvMsg', '已提交，AI 正在生成…', 'busy');
  api('/api/generate', { body: body }).then(function(r){
    if (!r.ok) showMsg(job === 'ov' ? 'ovMsg' : 'kvMsg', r.error || '提交失败', 'err');
  });
}

$('kvGen').onclick = function(){ startGen('kv'); };
$('ovGen').onclick = function(){ startGen('ov'); };

$('kvSave').onclick = function(){
  api('/api/save', { body: { kind: 'kv', fileName: $('kvFileName').value } }).then(function(r){
    if (r.ok) showMsg('kvMsg', '已保存：' + r.path, 'ok');
    else showMsg('kvMsg', r.error || '保存失败', 'err');
  });
};
$('ovSave').onclick = function(){
  api('/api/save', { body: { kind: 'ov', fileName: $('ovFileName').value } }).then(function(r){
    if (r.ok) showMsg('ovMsg', '已保存：' + r.path, 'ok');
    else showMsg('ovMsg', r.error || '保存失败', 'err');
  });
};
$('ovSaveSettings').onclick = function(){
  api('/api/save', { body: { kind: 'ovsettings', fileName: $('ovFileName').value } }).then(function(r){
    if (r.ok){
      showMsg('ovMsg',
        '已导出 CT 设置文件：' + r.path
        + '\n请完全关闭游戏后，把它复制并改名覆盖到：\n' + ($('ovTargetPath').textContent || '')
        + '\n再重新启动游戏即可看到覆盖物。', 'ok');
    } else showMsg('ovMsg', r.error || '导出失败', 'err');
  });
};

// ---------- 密钥保存 ----------
$('kAdd').onclick = function(){
  var body = {
    action: 'add',
    name: $('kName').value,
    baseUrl: $('kBase').value,
    model: $('kModel').value,
    apiKey: $('kKey').value
  };
  if (!body.baseUrl){ showMsg('kMsg', '请填写 Base URL', 'err'); return; }
  api('/api/keys', { body: body }).then(function(r){
    if (r.ok){ showMsg('kMsg', '已保存', 'ok'); $('kKey').value = ''; refreshState(); }
    else showMsg('kMsg', r.error || '保存失败', 'err');
  });
};

// ---------- 从 agent 配置导入密钥 ----------
$('kImportAgent').onclick = function(){
  api('/api/importagent', { body: { path: $('kAgentPath').value.trim() } }).then(function(r){
    if (r.ok){ showMsg('kAgentMsg', '已导入：' + r.name, 'ok'); refreshState(); }
    else showMsg('kAgentMsg', r.error || '导入失败', 'err');
  });
};

// ---------- 提示词与预设 ----------
var curPromptKey = null;

function loadPrompts(){
  api('/api/prompts').then(function(r){
    if (!r.ok) return;
    $('pDir').textContent = r.dir;
    var tb = $('pRows');
    tb.innerHTML = '';
    r.keys.forEach(function(k){
      var tr = document.createElement('tr');
      tr.innerHTML = '<td>' + esc(k.display) + '</td><td class=mono>' + esc(k.key) + '</td>'
        + '<td class=mono>' + (k.overridden ? '磁盘文件' : '内置默认') + '</td>'
        + '<td class=mono>' + k.length + '</td>';
      var td = document.createElement('td');
      td.appendChild(mkBtn('编辑', 'ghost', function(){ openPrompt(k.key); }));
      tr.appendChild(td);
      tb.appendChild(tr);
    });
  });
  loadPresets();
}

function openPrompt(key){
  api('/api/prompt?key=' + encodeURIComponent(key)).then(function(r){
    if (!r.ok){ showMsg('pMsg', r.error || '读取失败', 'err'); return; }
    curPromptKey = key;
    $('pEditCard').style.display = 'block';
    $('pEditTitle').textContent = '编辑提示词 · ' + (r.display || key) + '（' + key + '）';
    $('pEditHint').textContent = r.overridden ? '当前来源：磁盘文件' : '当前来源：内置默认';
    $('pText').value = r.text || '';
    hideMsg('pMsg');
  });
}

$('pSave').onclick = function(){
  if (!curPromptKey) return;
  api('/api/prompt', { body: { key: curPromptKey, text: $('pText').value } }).then(function(r){
    if (r.ok){ showMsg('pMsg', '已保存，下一次生成即生效', 'ok'); loadPrompts(); }
    else showMsg('pMsg', r.error || '保存失败', 'err');
  });
};

$('pReset').onclick = function(){
  if (!curPromptKey) return;
  if (!confirm('确定放弃磁盘上的修改，恢复内置默认提示词？')) return;
  api('/api/prompt', { body: { key: curPromptKey, reset: true } }).then(function(r){
    if (r.ok){ $('pText').value = r.text || ''; showMsg('pMsg', '已恢复内置默认', 'ok'); loadPrompts(); }
    else showMsg('pMsg', r.error || '操作失败', 'err');
  });
};

$('pReloadAll').onclick = function(){
  api('/api/prompts/reload', { body: {} }).then(function(r){
    showMsg('pStatus', r.ok ? '已从磁盘重载，下一次生成即生效' : (r.error || '重载失败'), r.ok ? 'ok' : 'err');
    loadPrompts();
  });
};

function loadPresets(){
  api('/api/presets').then(function(r){
    if (!r.ok) return;
    $('prPath').textContent = r.path;
    $('prText').value = r.text || '';
  });
}
$('prLoad').onclick = function(){ loadPresets(); hideMsg('prMsg'); };
$('prReset').onclick = function(){
  if (!confirm('确定恢复内置默认供应商预设？')) return;
  api('/api/presets', { body: { reset: true } }).then(function(r){
    if (r.ok){ $('prText').value = r.text || ''; showMsg('prMsg', '已恢复内置默认', 'ok'); }
    else showMsg('prMsg', r.error || '操作失败', 'err');
  });
};
$('prSave').onclick = function(){
  api('/api/presets', { body: { text: $('prText').value } }).then(function(r){
    if (r.ok){ $('prText').value = r.text || ''; showMsg('prMsg', '预设已保存并生效', 'ok'); }
    else showMsg('prMsg', r.error || '保存失败', 'err');
  });
};

// ---------- 更新与热重载 ----------
function loadUpdate(){
  api('/api/update').then(function(r){
    if (!r.ok) return;
    $('uCur').textContent = r.currentVersion || '-';
    $('uLatest').textContent = r.latestVersion || '（未检查）';
    $('uState').textContent = r.state + (r.busy ? '（进行中…）' : '');
    $('uCount').textContent = (r.reloadCount || 0) + ' 次';
    if (r.notes){ $('uNotes').style.display = 'block'; $('uNotes').textContent = r.notes; }
    if (r.error) showMsg('uMsg', r.error, 'err');
    if (r.bootstrapChanged) showMsg('uMsg', '引导器有改动，需重启游戏才完全生效', 'busy');
  });
}

$('uCheck').onclick = function(){
  showMsg('uMsg', '正在检查…', 'busy');
  api('/api/update/check', { body: {} }).then(function(r){
    if (!r.ok) showMsg('uMsg', r.error || '检查失败', 'err');
    setTimeout(loadUpdate, 1500);
  });
};
$('uDownload').onclick = function(){
  showMsg('uMsg', '正在下载更新包…', 'busy');
  api('/api/update/download', { body: {} }).then(function(r){
    if (!r.ok) showMsg('uMsg', r.error || '下载失败', 'err');
    setTimeout(loadUpdate, 1500);
  });
};
$('uApply').onclick = function(){
  if (!confirm('应用更新会覆盖核心程序集并立即热重载，确定继续？')) return;
  api('/api/update/apply', { body: {} }).then(function(r){
    if (r.ok) showMsg('uMsg', r.message || '已应用，稍后热生效', 'ok');
    else showMsg('uMsg', r.error || '应用失败', 'err');
  });
};
$('uReload').onclick = function(){
  api('/api/reloadcore', { body: {} }).then(function(r){
    if (r.ok) showMsg('uReloadMsg', r.message || '已请求热重载', 'ok');
    else showMsg('uReloadMsg', r.error || '请求失败', 'err');
  });
};

// ---------- 文件 ----------
function loadFiles(){
  api('/api/files').then(function(r){
    var tb = $('fileRows');
    tb.innerHTML = '';
    if (!r.ok){ showMsg('fMsg', r.error || '读取失败', 'err'); return; }
    if (!r.files.length){
      tb.innerHTML = '<tr><td colspan=5 class=mono>目录为空：' + esc(r.dir) + '</td></tr>';
      return;
    }
    r.files.forEach(function(f){
      var tr = document.createElement('tr');
      tr.innerHTML = '<td>' + esc(f.name) + '</td><td class=mono>' + esc(f.kind) + '</td>'
        + '<td class=mono>' + Math.round(f.size / 1024) + ' KB</td><td class=mono>' + esc(f.modified) + '</td>';
      var td = document.createElement('td');
      td.appendChild(mkBtn('查看', 'ghost', function(){ previewFile(f.name); }));
      td.appendChild(mkBtn('下载', 'ghost', function(){ downloadFile(f.name); }));
      if (f.kind === 'ctkv') td.appendChild(mkBtn('修正键雨', 'ghost', function(){ fixRain(f.name); }));
      td.appendChild(mkBtn('删除', 'ghost danger', function(){
        if (!confirm('确定删除 ' + f.name + '？')) return;
        api('/api/delete', { body: { name: f.name } }).then(function(){ loadFiles(); });
      }));
      tr.appendChild(td);
      tb.appendChild(tr);
    });
  });
}
function mkBtn(text, cls, fn){
  var b = document.createElement('button');
  b.className = cls; b.textContent = text; b.onclick = fn;
  return b;
}
function previewFile(name){
  api('/api/file?name=' + encodeURIComponent(name)).then(function(r){
    $('fPreviewCard').style.display = 'block';
    $('fPreviewTitle').textContent = '内容预览 · ' + name;
    $('fPreview').textContent = r.ok ? (r.content || '（空）') : (r.error || '读取失败');
  });
}
function downloadFile(name){
  location.href = '/api/download?name=' + encodeURIComponent(name) + '&t=' + encodeURIComponent(TOKEN);
}
function fixRain(name){
  api('/api/fixrain', { body: { name: name } }).then(function(r){
    showMsg('fMsg', r.ok ? '已修正 ' + name : (r.error || '修正失败'), r.ok ? 'ok' : 'err');
    loadFiles();
  });
}
$('fRefresh').onclick = loadFiles;
$('fOpen').onclick = function(){ api('/api/openfolder', { body: {} }); };

refreshState();
</script>
</body>
</html>";
    }
}

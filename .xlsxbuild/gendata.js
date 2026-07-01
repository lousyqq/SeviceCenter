const fs = require('fs');

const MONTH_WEEKS = [4, 4, 5, 4, 5, 4, 5, 4, 5, 4, 4, 5];
const months = [];
let ws = 1;
for (let m = 0; m < 12; m++) {
  months.push({ label: `2026${String(m + 1).padStart(2, '0')}`, start: ws, span: MONTH_WEEKS[m] });
  ws += MONTH_WEEKS[m];
}

const G = 'gold', GR = 'green', BL = 'blue', PK = 'pink';

// [No, category, owner, name, bars(start,end,text,color)]
const raw = [
  [1, 'FDC', '裕隆', 'b.[FDC Enhancement for RH/RL]', [[9, 13, 'SPEC 提供', G]]],
  [2, 'FDC', '裕隆', 'b.[2024QIT AAR版面&邏輯修改] BKM定義查詢與maintain', [[14, 22, 'SPEC 確認& 開發', G]]],
  [3, 'FDC', '裕隆', 'b.[2025QIT 跨廠FDC參數監控]', [[9, 18, 'SPEC 確認& 開發', G]]],
  [4, '防護', '裕隆', 'b.[E3 8.9 測試& 上線驗證: ESI Ap & Loader 確認]', [[5, 9, '驗證& 確認', G]]],
  [5, 'FDC', '裕隆', 'b.[MR+CL 1.35 Grp-Spec+CMS]', [[10, 18, 'SPEC 確認& 開發', G]]],
  [6, 'FDC', '裕隆', 'b.[MA Chart ET打定值(User Setting)]', [[5, 9, '測試開發確認', G]]],
  [7, '維運', '裕隆', 'b.[DB03 JOB 移轉]', [[5, 9, '確認& 驗證', G]]],
  [8, '防護', '裕隆', 'b.[BSL shift 過濾條件]', [[14, 26, 'SPEC 確認& 開發', G]]],
  [9, '專案', '裕隆', 'b.[2026 QIT 圍長]', [[5, 53, '', G]]],
  [10, 'FDC', '裕隆', 'b.[12M support]', [[5, 53, '', G]]],
  [11, 'FDC', '裕隆', 'b.[NODC 機器人]', [[14, 40, 'SPEC 確認& 開發', G]]],
  [12, '維運', '裕隆', 'c.[系統防護與維運]', [[5, 53, '系統防護與維運', G]]],
  [13, '專案', '裕隆', 'b.[POCDB]', [[27, 44, 'POCDB', G]]],
  [1, 'FDC', '玉婷', 'b.[MaxRange]: Retarget<10次', [[9, 13, '算法提供', G], [14, 18, '測試 & 上線', G]]],
  [2, 'FDC', '玉婷', 'b.[Retarget: Target超過HH.HL]', [[5, 9, 'SPEC提供', G], [19, 22, '算法調整', G]]],
  [3, 'FDC', '玉婷', 'b.[Tool Matching  for ca]', [[5, 8, 'Phase2開發', G], [9, 13, 'Phase3開發', G], [14, 22, 'Phase4開發', G], [23, 26, '通知Mail', G]]],
  [4, 'FDC', '玉婷', 'b.[BSL Shift Platform]', [[5, 13, '各區開發Hold', G], [14, 22, '回覆看版自動化開發', G], [23, 26, '測試&上線', G]]],
  [5, 'FDC', '玉婷', 'b.[Group SPEC]', [[45, 53, '轉自動化上線', G]]],
  [6, 'FDC', '玉婷', 'b.[RH/RL Platform]', [[41, 53, '加入OH/OL平台', G]]],
  [7, 'FDC', '玉婷', 'b.[MNOP]', [[5, 9, 'DB來源移轉', G], [10, 13, 'Phase1修改', G], [23, 26, 'Phase2修改', G]]],
  [8, 'FDC', '玉婷', 'b.[Warning Line Clear]', [[19, 22, '優化', G]]],
  [9, '跨廠', '玉婷', 'a.[12M support]', [[5, 22, '程式碼提供', G]]],
  [11, '數轉', '玉婷', 'b.[IND 4.0 & 數位轉型-代辦中心網頁]', [[19, 26, '網頁建置', G]]],
  [13, '數轉', '玉婷', 'b.[GptDB 大盤]', [[18, 26, '大盤Excel資料提供', G]]],
  [12, '跨廠', '玉婷', 'b.12M EQDashboard', [[14, 26, '自主版面設計&調整', G]]],
  [10, '維運', '玉婷', 'c.[TempSpec系統維運]', [[5, 53, '系統維運', G]]],
  [14, '維運', '玉婷', 'b.[系統日常]', [[5, 53, '日常指標確認', G]]],
  [15, 'FDC', '玉婷', 'b. 大氣壓力Auto Mail', [[23, 26, '新增Mail', G]]],
  [16, 'FDC', '玉婷', 'b. EQDashboard ZE看板', [[27, 31, 'BWH版面調整', G], [32, 35, 'WL新增史高低', G]]],
  [1, '設備', '詠裕', 'b.[2026 APM]', [[1, 5, 'ESI Server APM', G]]],
  [2, '設備', '詠裕', 'b.[2027 ESI Server 採購]', [[5, 35, '', G]]],
  [3, '跨廠', '詠裕', 'b.[12M support]', [[1, 22, '程式碼提供', G]]],
  [4, '防護', '詠裕', 'b.[12W support]', [[18, 40, 'eUSPC SQL', G]]],
  [5, 'FDC', '詠裕', 'b.[eFDC 應用擴展]', [[1, 13, '開發環境建置 & 取資料API開發', G], [18, 26, '自動擷取使用者資訊', G], [31, 40, '曲圖API開發', G], [41, 48, '轉 Local DB', G], [49, 53, '權限控管與分流', G]]],
  [6, 'FDC', '詠裕', 'b.[Extra Sensor 看板]', [[5, 17, 'Phase1開發', G], [41, 48, 'Phase1開發', G], [49, 53, 'Phase2開發', G]]],
  [7, 'FDC', '詠裕', 'b.[Raw Trace]', [[9, 22, '', G]]],
  [8, 'FDC', '詠裕', 'b.[Tool Log]', [[41, 53, 'LRTP 分析工具開發', G]]],
  [9, '設備', '詠裕', 'b.[2026 QIT - Chart Data]', [[1, 13, 'FDC Chart 資料預置 & PDCA', G], [18, 26, '預先產生FDC縮圖', G]]],
  [10, '設備', '詠裕', 'b.[GPTDB-公用系統環境建置、使用者權限設定]', [[5, 13, '零基礎環境設定', G]]],
  [11, 'FDC', '詠裕', 'b.[Warning Line]', [[14, 22, 'Min Scale 設定上架', G]]],
  [12, '設備', '詠裕', 'b.[系統防護與維運]', [[1, 22, 'Server 資源盤點、移轉、高風險 server 下線', G]]],
  [1, 'FDC', '宸詳', 'b.[FDC Early Detection - Chart Audit 3.0]', [[1, 13, 'Type1 &Type2 自動上線 & Ma', G], [14, 22, 'Type 3~6 待併傷SPEC', G]]],
  [2, 'QIT', '宸詳', 'b.[2026 QIT - Chart List]', [[1, 13, 'Chart List 開發 & 驗證', G], [14, 18, '待 PDCA', G], [19, 26, '新需求(HH/HL)', G]]],
  [3, 'FDC', '宸詳', 'b.[IND 4.0 & 數位轉型 - DB 建置]', [[14, 26, '需求調查 & POC & DB 建置', G]]],
  [4, '設備', '宸詳', 'b.[2026 APM]', [[1, 5, 'ESI Server APM', G]]],
  [5, '防護', '宸詳', 'c.[系統防護 - 指標 vs. AMSD]', [[14, 26, '與IT討論系統防護項目', G]]],
  [6, '防護', '宸詳', 'c.[系統防護 - Server EOS 系統移轉&調校]', [[5, 17, 'EOS Server 系統移轉', G]]],
  [7, '設備', '宸詳', 'b.[IND 4.0 & 數位轉型 - 貼片 All in one Web API]', [[14, 22, 'WebAPI 開發', G]]],
  [8, '跨廠', '宸詳', 'b.[12M support]', [[1, 13, '程式碼提供', G], [18, 31, '系統建置', G]]],
  [9, 'FDC', '宸詳', 'a.[Cross FAB - 需求控管表]', [[5, 17, 'MSD 需求追蹤 & 確認', G]]],
  [10, 'FDC', '宸詳', 'b.[系統日管]', [[5, 53, '日管指標確認', G]]],
  [1, '設備', '政翰', 'a.[2026 APM]', [[1, 5, 'ESI Server APM', G]]],
  [2, 'FDC', '政翰', 'a.[2026 QIT - 副圍長]', [[1, 13, 'S1~S2 檢視其出數值與PPT格式', G], [18, 26, 'S3~S5 真因邏輯檢視與PPT Format 訂版', GR], [31, 35, 'S5', G], [45, 53, 'S6~S8', G]]],
  [3, 'FDC', '政翰', 'b.[CMS Warning Line]', [[1, 13, '(2) 開發新Warning Limit-->RH/RL偵測模組', G], [18, 26, '2Y資料做WL SPEC', GR]]],
  [4, '維運', '政翰', 'u0 u10', [[1, 5, '系統維運', G], [6, 13, 'RTO Case 查詢', GR], [14, 26, '機台異常OCAP運作流程 & ENS Phone Call Tracing', G], [45, 53, '系統維運', G]]],
  [5, '維運', '政翰', 'c.[CMS] Server Redundant', [[1, 9, '備援Server建立', G]]],
  [6, 'FDC', '政翰', 'b.[WL: 移除天條限制](new)', [[18, 26, '移除天條限制', G]]],
  [7, 'FDC', '政翰', 'b.[WL: Auto Tighen Enhancement]', [[1, 5, '流程修正', G], [6, 17, 'Alarm & Target信件功能 & 移除Target 偏差不上線條件', G]]],
  [8, 'FDC', '政翰', 'b.[WL: Auto Tighen Job]', [[1, 13, 'ET12 -> ET34 -> LT -> DF -> TF -> LT', G], [14, 26, 'ET12 -> LT -> DF ->ET34 -> TF -> LT', G]]],
  [9, '數轉', '政翰', 'b.[GenAI - PoCDB & API 應用]', [[1, 9, 'GenAI DB&API 代辦中心', G], [14, 22, '插頭 & 插座資料彙整', G], [27, 40, '回收使用成果與效益', G]]],
  [10, '數轉', '政翰', 'b.[FDC & NODC 機器人]', [[6, 22, 'Spec 確認、技能Study、功能開發、測試', G], [27, 40, '功能開發、測試', G], [45, 53, '功能調整&上線', G]]],
  [11, '維運', '政翰', 'b.[Server Job 轉移作業]', [[1, 9, '系統轉移完軍', G]]],
  [12, '跨廠', '政翰', 'b.[12M support]', [[1, 13, '程式碼提供', G]]],
  [1, '維運', '冠芝', 'b.[DB03 JOB 移轉]', [[1, 9, '系統移轉完軍', G]]],
  [2, '跨廠', '冠芝', 'a. Project West-FDC support', [[1, 9, 'ata veiw from Manual transmis', BL], [14, 40, 'Automatic transmission', BL], [41, 53, 'Index 管理機制', PK]]],
  [3, '防護', '冠芝', 'c.FD over 120 sec', [[1, 9, '上線', G], [14, 22, '效果確認', GR]]],
  [4, '設備', '冠芝', 'c.[DF Tuning System]', [[1, 13, '回補2025各tool type小需求', G], [18, 53, '系統維運', G]]],
  [5, 'FDC', '冠芝', 'b.[2026 QIT - 輔導]', [[1, 53, '輔導', G]]],
];

const owners = [];
const projects = raw.map((r, i) => {
  const [no, category, owner, name, bars] = r;
  if (!owners.includes(owner)) owners.push(owner);
  return {
    id: 'P' + String(i + 1).padStart(3, '0'),
    no, owner, category, name,
    bars: bars.map(([start, end, text, color]) => ({ start, end, text, color })),
  };
});

const summary = [
  'E3 防守與維護(7)', '系統維運與穩定(7)', 'FDC防護網強化(5)',
  'Gen AI 應用與 GPTDB 建置(5)', '2026 QIT效率提升(5)', '跨廠支援12M(6)/12W(1)', '設備流程改善(1)',
];

const data = { year: 2026, weeks: 53, months, owners, projects, summary };
const out = 'window.APP_DATA = ' + JSON.stringify(data, null, 0) + ';\n';
fs.writeFileSync('C:/CattTask/ServiceCenter/wwwroot/progress/projects.data.js', out);
fs.writeFileSync('C:/CattTask/ServiceCenter/wwwroot/progress/projects.json', JSON.stringify(data, null, 2));
console.log('projects:', projects.length, 'owners:', owners.join(','));

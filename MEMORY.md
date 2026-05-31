# MEMORY.md - 開發與系統記憶文件

此檔案記錄專案過程中的關鍵決策、踩坑紀錄、與程式碼慣例，方便後續 AI 助理與開發者維護與參考。

## 系統核心邏輯與慣例 (Rules & Conventions)

### 1. 資料篩選與統計邏輯 (Filtering & Stats Rules)
- **狀態判定 (Status)**: 
  - 任務的狀態判定必須使用**「包含比對 (includes)」**而非「嚴格等於 (===)」。
  - 例：檢查「已上線」應使用 `status.includes('已上線')`，以便相容 `9.已上線` 等字串。
  - 同理適用於「處理中」(`status.includes('處理中')`) 及「確認中」(`status.includes('確認中')`)。
- **標籤分隔 (DBCat)**:
  - `DBCat` 欄位常常會用來表示系統使用的工具或語言，在畫面上會渲染為標籤。
  - 拆分標籤時，支援逗號 `,`（包含全形 `，`）與反斜線 `/`（包含全形 `／`）。
  - 正規表示式：`split(/[,\/]/)`。

### 2. 資料庫操作 (Database Conventions)
- **環境設定**:
  - 資料庫為 `SQL Server`，主要庫名為 `WEB`。
  - 本地連線字串為：`Data Source=Sariel;Initial Catalog=WEB;User ID=testuser;Password=test;TrustServerCertificate=True;`
- **匯入邏輯 (Excel Import) 與防呆機制**:
  - **欄位無序對應 (防呆)**：透過比對 Header 名稱與 index 建立 `colMap`，因此 Excel 內的欄位順序可以隨意調換。
  - **多工作表處理 (防呆)**：固定讀取 `workbook.SheetNames[0]`，確保永遠只取最靠左的第一張表。
  - **未知欄位 Bypass (防呆)**：在 `colMap` 對應邏輯中，若出現無法辨識的欄位名稱將會直接略過不處理，不會影響已知欄位的匯入與系統運作。
  - 執行 Excel 匯入 (`POST /api/tasks/import`) 時，會直接使用 `DELETE FROM TaskCenter` 與 `DELETE FROM TaskStation` 清空資料表，並用 `DBCC CHECKIDENT ('TaskCenter', RESEED, 0)` 重置 RegId 自增值。
  - 必須確保 `TaskCenter` 及 `TaskStation` 皆已在資料庫中確實建立 (schema 正確)，否則在清空或重新 Insert 時會產生 HTTP 500 報錯。

### 3. 前端錯誤處理 (Frontend Error Handling)
- 若 API 回傳 500 等失敗狀態碼，切記確保 try/catch 中的變數（如 `payloads`）有正確的提升（hoisting）或宣告在 block 外層，以免拋出 `ReferenceError: payloads is not defined` 蓋過原本的 API 錯誤訊息。

## 歷史修改摘要 (Recent Updates)
1. **[2026-05-27] 新增 Excel 匯入功能與 Bug 修復**: 
   - 修正了 `index.html` 內匯入失敗時 `payloads` 變數作用域出錯的問題。
   - 建立了 `TaskCenter` 的 Table Schema。
2. **[2026-05-27] 放寬狀態篩選條件與標籤支援**:
   - 解決 KPI 卡片與統計報表無法顯示 `9.已上線` 任務數量的問題，全面統一採用 `%已上線%` 的 `includes` 邏輯。
   - 讓 `DBCat` 的標籤分隔符號同時支援 `/` 與 `,`。

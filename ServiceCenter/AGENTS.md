# ServiceCenter (代辦中心) - GENAI EMPOWERED TASK CONTROL

## 專案概述 (Project Overview)
本專案為一個代辦任務追蹤與管理系統，主要用途為管理各部門的專案開發進度、需求分派、系統狀態以及模組(Station/Module)使用統計。提供現代化響應式 UI 與豐富的資料視覺化統計圖表。

## 技術架構 (Technology Stack)
- **前端 (Frontend)**: 
  - 純 HTML5 + Vanilla JavaScript (無框架)
  - 樣式採用 Tailwind CSS (透過 CDN 載入)
  - 圖示採用 Lucide Icons
  - Excel 解析使用 SheetJS (xlsx)
- **後端 (Backend)**: 
  - .NET Minimal API (C#)
  - 使用 `Microsoft.Data.SqlClient` 進行資料庫操作
- **匯入邏輯 (Excel Import) 與防呆機制**:
  - **欄位無序對應**：透過比對 Header 名稱與 index 建立 `colMap`，因此 Excel 內的欄位順序可以隨意調換。
  - **多工作表處理**：寫死讀取 `workbook.SheetNames[0]`，確保固定只取最靠左的第一張表。
  - **未知欄位 Bypass**：在對應邏輯中，若出現無法辨識的欄位名稱，將會直接被略過不處理，不影響已知欄位的匯入。
  - 執行 Excel 匯入 (`POST /api/tasks/import`) 時，會直接使用 `DELETE FROM TaskCenter` 與 `DELETE FROM TaskStation` 清空資料表後重新寫入。
    - **匯入成功後清空 Upload 資料夾**：交易 commit 成功後呼叫 `ClearUploadDir()`，刪除 `wwwroot/Upload/` 內所有實體檔，避免上一批附件殘留累積。採 best-effort（被鎖住的檔略過、不中斷）。注意 Excel 只帶回 `Attachment` 路徑字串、不含實體檔，故清檔後匯入資料的附件連結會失效，屬預期行為。
    - **注意（測試階段設計）**：`TaskCenter` 已改為「無 Key、無 IDENTITY」的純資料表，因此 `RegId` 不再是自增欄位。匯入時 `RegId` 直接採用 Excel payload 的值（若缺值才以序號遞補），不再執行 `DBCC CHECKIDENT`；新增單筆時則以 `SELECT ISNULL(MAX(RegId),0)+1` 取得新號。
- **資料庫 (Database)**: 
  - SQL Server (資料庫名稱: `WEB`)
  - 核心資料表:
    - `[dbo].[TaskCenter]`: 儲存主要任務資料。**測試階段為純 `CREATE TABLE`、所有欄位皆 NULL、無 PRIMARY KEY / IDENTITY / DEFAULT / index**，以便任何人都能自由調整表格內容（schema 定義見 `sql架構.txt`）。
      - 欄位：`RegDate, RegId, NotesID, Status, Owner, DBCat, Description, Applicant, Department, Section, TCD, Benefit, AppLink, DataSource, CompletionDate, Remark, RemarkStatus, Attachment`
      - `Attachment` 為 `nvarchar(max)`，儲存上傳檔案的相對路徑（例：`/Upload/{guid}_{name}.xlsx`）。
    - `[dbo].[TaskStation]`: 儲存任務關聯的插座/模組資料 (StationName, MpValue, UrlLink)。**本版本未修改。**

## 主要功能 (Core Features)
1. **任務管理**:
   - 新增、修改、刪除任務 (CRUD)。
   - 支援清單 (Table) 與卡片 (Card) 雙視圖切換。
   - 提供各欄位的動態排序與篩選功能。
   - **MP 欄＝站點 MP 總和**：任務清單的 `MP` 欄 (`benefit`) 不再獨立存值，而是在 `mapDBtoFrontend` 攤平站點後，由 17 個站點欄位 (DF1–PTI) 的 MP 值即時加總而得（取各站值 `'^'` 前的數字、`Math.round(sum*10000)/10000` 去浮點尾數）。因此 MP 欄、KPI 總效益、站點明細的「合計 MP」、匯出 Excel 全部一致。`'mp^url'` 中的 url 不計入加總。
   - **站點 MP 明細展開（DF1–PTI）**：任務清單中點擊「需求摘要」文字，會在該列下方展開（再點收合）一個隱藏列，以晶片 (chip) grid 呈現該筆任務 17 個站點欄位 (`DF1,DF2,DF3,DF4,ET1,ET2,ET3,ET4,LT3,LT4,TF1,TF2,TF3,TF4,TF5,TF6,PTI`) 的 MP 值，並標示合計 MP（= 網頁 MP 欄位）。各站點值來自 `mapDBtoFrontend` 攤平的 `row[col.toLowerCase()]`（格式 `''` / `'mp'` / `'mp^url'`，url 以外部連結 icon 呈現），無值顯示 `-`。實作：`toggleStationDetail(regId, btnEl)` 切換 `tr.station-detail-row[data-detail-for]` 的 `hidden`，並旋轉 chevron 圖示。此設計避免把 17 個站點欄位直接平鋪在主表造成擁擠。
2. **Excel 大量匯入 (Import) 與防呆機制**:
   - 支援讀取 Excel 檔案，自動配對欄位並匯入至 SQL Server。
   - **防呆機制 1**：動態對應欄位，無論 Excel 中的欄位順序如何調換皆可準確抓取。
   - **防呆機制 2**：若檔案含有多個工作表，系統固定只抓取「最靠左的第一個工作表」。
   - **防呆機制 3**：忽略不認識的未知欄位資料（Bypass），僅針對系統能辨識的欄位進行轉化與匯入，不會影響整體運作。
   - 匯入時若缺少部分必要欄位，系統具備自動給予預設值 (如：缺欄位改為'確認中')。
3. **動態狀態查詢**:
   - 狀態篩選支援「模糊比對 / 包含字眼 (includes)」，例如篩選 `%已上線%`、`%處理中%`。
4. **統計報表與 KPI**:
   - 總需求數量、各狀態專案數量、預估總效益 (MP)。
   - 依照不同部門/課別統計各插座與 Module 的使用數量。
5. **UI / UX 強化**:
   - `DBCat` 欄位支援逗點 `,` 及斜線 `/` 自動分割為精美的標籤 (Tags)。
   - 支援深色模式 (Dark Mode) 與淺色模式 (Light Mode)。
6. **檔案上傳 (Attachment Upload)**:
   - 新增/編輯需求的 Modal 最底下（需求摘要下方）提供「上傳檔案(非必填)」列，支援 Excel (`.xlsx/.xls`)、PPT (`.ppt/.pptx`)、圖檔 (`.png/.jpg/.jpeg/.gif/.bmp/.webp`)。
   - 後端端點：
     - `POST /api/upload`：以 `multipart/form-data` 接收欄位 `file`，驗證副檔名，存入 `wwwroot/Upload/`，回傳 `{ path, name }`。
     - `DELETE /api/upload?path=/Upload/xxx`：刪除指定上傳檔。
     - 兩者皆透過 `Path.GetFileName()` + 強制 `/Upload/` 前綴檢查防止路徑穿越 (path traversal)。
   - **唯一檔名規則（防誤刪核心）＋ 任務編號前綴**：實際存檔名為 `{RegId}_{Guid:N}_{安全化原檔名}{副檔名}`。
     - **上傳階段**：`POST /api/upload` 此時尚不知 RegId，先存成 `{Guid:N}_{安全化原檔名}{副檔名}`。
     - **儲存階段補上 RegId**：新增 (`POST /api/tasks`，RegId 由 `MAX+1` 決定) 與編輯 (`PUT /api/tasks/{id}`，RegId 已知) 時，後端 `ApplyRegIdToAttachment(regId, path)` 會把實體檔改名為 `{RegId}_{Guid:N}_…`，並把新路徑寫回 `Attachment`。此函式以正則 `^\d+_` 去除既有 RegId 前綴再補新值，故**具冪等性**（重複儲存不會疊加前綴）；GUID 為 16 進位不會被 `^\d+_` 誤判。
     - GUID 保證不同任務同名檔不碰撞、不誤刪；RegId 前綴讓檔名（含 DB / 匯出 Excel）能直接對應到資料列。
     - 新增 = 產生 GUID 檔後補 RegId；編輯重新上傳 = 先上傳新 GUID 檔、依「精準路徑」刪舊檔，儲存時補 RegId；編輯刪除 = 依精準路徑刪檔並清空欄位；刪除任務 = 先讀出 `Attachment` 再刪對應檔。
   - **顯示一律去前綴**：前端 `getAttachmentDisplayName(path)` 以正則 `^(?:\d+_)?[0-9a-fA-F]{32}_` 去掉 `{RegId}_` 與 `{Guid}_` 前綴，畫面/tooltip 只顯示原始檔名（例：`53_3822…c6_TEST.xlsx` → `TEST.xlsx`）。`href` 仍指向真實檔案路徑。
   - **任務清單顯示**：`Attachment` 欄位以檔案類型 icon 呈現（Excel / PPT / 圖檔各有對應 Lucide 圖示與顏色），hover tooltip 顯示去前綴後的原始檔名，點擊可於新分頁開啟。

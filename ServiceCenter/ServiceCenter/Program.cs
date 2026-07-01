using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System;
using System.Data;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// ===== 請填入您的 MS SQL 連線字串 =====
string connStr = "Data Source=Sariel;Initial Catalog=WEB;User ID=testuser;Password=test;TrustServerCertificate=True; ";

// 1. 取得所有資料
app.MapGet("/api/tasks", async () =>
{
    var tasks = new Dictionary<int, TaskDto>();
    using var conn = new SqlConnection(connStr);
    await conn.OpenAsync();

    var query = @"
        SELECT t.RegId, t.RegDate, t.CompletionDate, t.NotesID, t.Status, t.Department, t.Section, t.Applicant, t.Description,
               t.Owner, t.Benefit, t.DBCat, t.TCD, t.AppLink, t.DataSource, t.Remark, t.RemarkStatus, t.Attachment,
               s.StationName, s.MpValue, s.UrlLink
        FROM TaskCenter t
        LEFT JOIN TaskStation s ON t.RegId = s.TaskRegId
        ORDER BY t.RegId ASC";

    using var cmd = new SqlCommand(query, conn);
    using var reader = await cmd.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        int regId = (int)reader["RegId"];
        if (!tasks.ContainsKey(regId))
        {
            tasks[regId] = new TaskDto
            {
                RegId = regId,
                Date = reader["RegDate"] != DBNull.Value ? Convert.ToDateTime(reader["RegDate"]).ToString("yyyy-MM-dd") : "",
                CompletionDate = reader["CompletionDate"] != DBNull.Value ? Convert.ToDateTime(reader["CompletionDate"]).ToString("yyyy-MM-dd") : "",
                NotesId = reader["NotesID"].ToString(),
                Status = reader["Status"].ToString(),
                Dept = reader["Department"].ToString(),
                Sec = reader["Section"].ToString(),
                Applicant = reader["Applicant"].ToString(),
                Desc = reader["Description"].ToString(),
                Owner = reader["Owner"].ToString(),
                Benefit = reader["Benefit"] != DBNull.Value ? Convert.ToDouble(reader["Benefit"]) : 0,
                DbCat = reader["DBCat"].ToString(),
                Tcd = reader["TCD"].ToString(),
                AppLink = reader["AppLink"].ToString(),
                DataSource = reader["DataSource"].ToString(),
                Remark = reader["Remark"].ToString(),
                RemarkStatus = reader["RemarkStatus"].ToString(),
                Attachment = reader["Attachment"].ToString(),
                Stations = new List<StationDto>()
            };
        }

        if (reader["StationName"] != DBNull.Value)
        {
            tasks[regId].Stations.Add(new StationDto
            {
                StationName = reader["StationName"].ToString(),
                MpValue = reader["MpValue"].ToString(),
                UrlLink = reader["UrlLink"].ToString()
            });
        }
    }
    return Results.Ok(tasks.Values);
});

// 2. 新增單筆資料
app.MapPost("/api/tasks", async (TaskDto task) =>
{
    using var conn = new SqlConnection(connStr);
    await conn.OpenAsync();
    using var transaction = conn.BeginTransaction();
    try
    {
        // RegId 已非自動遞增，於此計算下一個可用編號
        using var cmdMax = new SqlCommand("SELECT ISNULL(MAX(RegId), 0) + 1 FROM TaskCenter", conn, transaction);
        int newId = (int)await cmdMax.ExecuteScalarAsync();

        // 取得 RegId 後，把附件實體檔改名為 {RegId}_{GUID}_原檔名，並改用新路徑存入 DB
        task.Attachment = ApplyRegIdToAttachment(newId, task.Attachment);

        using var cmd = new SqlCommand(@"
            INSERT INTO TaskCenter
            (RegId, RegDate, CompletionDate, NotesID, Status, Department, Section, Applicant, Description, Owner, Benefit, DBCat, TCD, AppLink, DataSource, Remark, RemarkStatus, Attachment)
            VALUES
            (@RegId, @RegDate, @CompletionDate, @NotesId, @Status, @Dept, @Sec, @Applicant, @Desc, @Owner, @Benefit, @DbCat, @Tcd, @AppLink, @DataSource, @Remark, @RemarkStatus, @Attachment)", conn, transaction);

        // 強制指定 SqlDbType 避免 NULL 轉型失敗
        cmd.Parameters.Add("@RegId", SqlDbType.Int).Value = newId;
        cmd.Parameters.Add("@RegDate", SqlDbType.Date).Value = string.IsNullOrWhiteSpace(task.Date) ? DBNull.Value : task.Date;
        cmd.Parameters.Add("@CompletionDate", SqlDbType.Date).Value = string.IsNullOrWhiteSpace(task.CompletionDate) ? DBNull.Value : task.CompletionDate;
        cmd.Parameters.Add("@NotesId", SqlDbType.NVarChar).Value = task.NotesId ?? (object)DBNull.Value;
        cmd.Parameters.Add("@Status", SqlDbType.NVarChar).Value = task.Status ?? (object)DBNull.Value;
        cmd.Parameters.Add("@Dept", SqlDbType.NVarChar).Value = task.Dept ?? (object)DBNull.Value;
        cmd.Parameters.Add("@Sec", SqlDbType.NVarChar).Value = task.Sec ?? (object)DBNull.Value;
        cmd.Parameters.Add("@Applicant", SqlDbType.NVarChar).Value = task.Applicant ?? (object)DBNull.Value;
        cmd.Parameters.Add("@Desc", SqlDbType.NVarChar).Value = task.Desc ?? (object)DBNull.Value;
        cmd.Parameters.Add("@Owner", SqlDbType.NVarChar).Value = task.Owner ?? (object)DBNull.Value;
        cmd.Parameters.Add("@Benefit", SqlDbType.Float).Value = task.Benefit;
        cmd.Parameters.Add("@DbCat", SqlDbType.NVarChar).Value = task.DbCat ?? (object)DBNull.Value;
        cmd.Parameters.Add("@Tcd", SqlDbType.NVarChar).Value = task.Tcd ?? (object)DBNull.Value;
        cmd.Parameters.Add("@AppLink", SqlDbType.NVarChar).Value = task.AppLink ?? (object)DBNull.Value;
        cmd.Parameters.Add("@DataSource", SqlDbType.NVarChar).Value = task.DataSource ?? (object)DBNull.Value;
        cmd.Parameters.Add("@Remark", SqlDbType.NVarChar).Value = task.Remark ?? (object)DBNull.Value;
        cmd.Parameters.Add("@RemarkStatus", SqlDbType.NVarChar).Value = task.RemarkStatus ?? (object)DBNull.Value;
        cmd.Parameters.Add("@Attachment", SqlDbType.NVarChar).Value = task.Attachment ?? (object)DBNull.Value;

        await cmd.ExecuteNonQueryAsync();

        if (task.Stations != null)
        {
            foreach (var st in task.Stations)
            {
                using var cmdSt = new SqlCommand("INSERT INTO TaskStation (TaskRegId, StationName, MpValue, UrlLink) VALUES (@RegId, @StationName, @MpValue, @UrlLink)", conn, transaction);
                cmdSt.Parameters.AddWithValue("@RegId", newId);
                cmdSt.Parameters.AddWithValue("@StationName", st.StationName ?? "");
                cmdSt.Parameters.AddWithValue("@MpValue", st.MpValue ?? "");
                cmdSt.Parameters.AddWithValue("@UrlLink", st.UrlLink ?? "");
                await cmdSt.ExecuteNonQueryAsync();
            }
        }
        transaction.Commit();
        return Results.Ok(new { success = true, regId = newId });
    }
    catch (Exception ex)
    {
        transaction.Rollback();
        return Results.Problem(ex.Message);
    }
});

// 3. 更新單筆資料
app.MapPut("/api/tasks/{id}", async (int id, TaskDto task) =>
{
    using var conn = new SqlConnection(connStr);
    await conn.OpenAsync();
    using var transaction = conn.BeginTransaction();
    try
    {
        // 編輯若有新上傳的附件 ({GUID}_原檔名)，補上此任務的 RegId 前綴；已含正確前綴者不動
        task.Attachment = ApplyRegIdToAttachment(id, task.Attachment);

        using var cmd = new SqlCommand(@"
            UPDATE TaskCenter SET
                RegDate=@RegDate, CompletionDate=@CompletionDate, NotesID=@NotesId, Status=@Status, Department=@Dept,
                Section=@Sec, Applicant=@Applicant, Description=@Desc,
                Owner=@Owner, Benefit=@Benefit, DBCat=@DbCat, TCD=@Tcd,
                AppLink=@AppLink, DataSource=@DataSource, Remark=@Remark, RemarkStatus=@RemarkStatus, Attachment=@Attachment
            WHERE RegId=@RegId", conn, transaction);

        cmd.Parameters.Add("@RegId", SqlDbType.Int).Value = id;
        cmd.Parameters.Add("@RegDate", SqlDbType.Date).Value = string.IsNullOrWhiteSpace(task.Date) ? DBNull.Value : task.Date;
        cmd.Parameters.Add("@CompletionDate", SqlDbType.Date).Value = string.IsNullOrWhiteSpace(task.CompletionDate) ? DBNull.Value : task.CompletionDate;
        cmd.Parameters.Add("@NotesId", SqlDbType.NVarChar).Value = task.NotesId ?? (object)DBNull.Value;
        cmd.Parameters.Add("@Status", SqlDbType.NVarChar).Value = task.Status ?? (object)DBNull.Value;
        cmd.Parameters.Add("@Dept", SqlDbType.NVarChar).Value = task.Dept ?? (object)DBNull.Value;
        cmd.Parameters.Add("@Sec", SqlDbType.NVarChar).Value = task.Sec ?? (object)DBNull.Value;
        cmd.Parameters.Add("@Applicant", SqlDbType.NVarChar).Value = task.Applicant ?? (object)DBNull.Value;
        cmd.Parameters.Add("@Desc", SqlDbType.NVarChar).Value = task.Desc ?? (object)DBNull.Value;
        cmd.Parameters.Add("@Owner", SqlDbType.NVarChar).Value = task.Owner ?? (object)DBNull.Value;
        cmd.Parameters.Add("@Benefit", SqlDbType.Float).Value = task.Benefit;
        cmd.Parameters.Add("@DbCat", SqlDbType.NVarChar).Value = task.DbCat ?? (object)DBNull.Value;
        cmd.Parameters.Add("@Tcd", SqlDbType.NVarChar).Value = task.Tcd ?? (object)DBNull.Value;
        cmd.Parameters.Add("@AppLink", SqlDbType.NVarChar).Value = task.AppLink ?? (object)DBNull.Value;
        cmd.Parameters.Add("@DataSource", SqlDbType.NVarChar).Value = task.DataSource ?? (object)DBNull.Value;
        cmd.Parameters.Add("@Remark", SqlDbType.NVarChar).Value = task.Remark ?? (object)DBNull.Value;
        cmd.Parameters.Add("@RemarkStatus", SqlDbType.NVarChar).Value = task.RemarkStatus ?? (object)DBNull.Value;
        cmd.Parameters.Add("@Attachment", SqlDbType.NVarChar).Value = task.Attachment ?? (object)DBNull.Value;

        await cmd.ExecuteNonQueryAsync();

        using var cmdDelSt = new SqlCommand("DELETE FROM TaskStation WHERE TaskRegId=@RegId", conn, transaction);
        cmdDelSt.Parameters.AddWithValue("@RegId", id);
        await cmdDelSt.ExecuteNonQueryAsync();

        if (task.Stations != null)
        {
            foreach (var st in task.Stations)
            {
                using var cmdSt = new SqlCommand("INSERT INTO TaskStation (TaskRegId, StationName, MpValue, UrlLink) VALUES (@RegId, @StationName, @MpValue, @UrlLink)", conn, transaction);
                cmdSt.Parameters.AddWithValue("@RegId", id);
                cmdSt.Parameters.AddWithValue("@StationName", st.StationName ?? "");
                cmdSt.Parameters.AddWithValue("@MpValue", st.MpValue ?? "");
                cmdSt.Parameters.AddWithValue("@UrlLink", st.UrlLink ?? "");
                await cmdSt.ExecuteNonQueryAsync();
            }
        }
        transaction.Commit();
        return Results.Ok(new { success = true });
    }
    catch (Exception ex)
    {
        transaction.Rollback();
        return Results.Problem(ex.Message);
    }
});

// 4. 批次匯入 (覆蓋現有資料)
app.MapPost("/api/tasks/import", async (List<TaskDto> tasks) =>
{
    using var conn = new SqlConnection(connStr);
    await conn.OpenAsync();
    using var transaction = conn.BeginTransaction();
    try
    {
        // 刪除舊資料
        using var cmdDelStation = new SqlCommand("DELETE FROM TaskStation", conn, transaction);
        await cmdDelStation.ExecuteNonQueryAsync();
        using var cmdDelCenter = new SqlCommand("DELETE FROM TaskCenter", conn, transaction);
        await cmdDelCenter.ExecuteNonQueryAsync();

        int seq = 0;
        foreach (var task in tasks)
        {
            // RegId 優先採用 Excel 提供的編號，若無則依序遞增
            int newId = task.RegId > 0 ? task.RegId : ++seq;
            seq = newId;

            using var cmdCenter = new SqlCommand(@"
                INSERT INTO TaskCenter
                (RegId, RegDate, CompletionDate, NotesID, Status, Department, Section, Applicant, Description, Owner, Benefit, DBCat, TCD, AppLink, DataSource, Remark, RemarkStatus, Attachment)
                VALUES
                (@RegId, @RegDate, @CompletionDate, @NotesId, @Status, @Dept, @Sec, @Applicant, @Desc, @Owner, @Benefit, @DbCat, @Tcd, @AppLink, @DataSource, @Remark, @RemarkStatus, @Attachment)", conn, transaction);

            cmdCenter.Parameters.Add("@RegId", SqlDbType.Int).Value = newId;
            cmdCenter.Parameters.Add("@RegDate", SqlDbType.Date).Value = string.IsNullOrWhiteSpace(task.Date) ? DBNull.Value : task.Date;
            cmdCenter.Parameters.Add("@CompletionDate", SqlDbType.Date).Value = string.IsNullOrWhiteSpace(task.CompletionDate) ? DBNull.Value : task.CompletionDate;
            cmdCenter.Parameters.Add("@NotesId", SqlDbType.NVarChar).Value = task.NotesId ?? (object)DBNull.Value;
            cmdCenter.Parameters.Add("@Status", SqlDbType.NVarChar).Value = task.Status ?? (object)DBNull.Value;
            cmdCenter.Parameters.Add("@Dept", SqlDbType.NVarChar).Value = task.Dept ?? (object)DBNull.Value;
            cmdCenter.Parameters.Add("@Sec", SqlDbType.NVarChar).Value = task.Sec ?? (object)DBNull.Value;
            cmdCenter.Parameters.Add("@Applicant", SqlDbType.NVarChar).Value = task.Applicant ?? (object)DBNull.Value;
            cmdCenter.Parameters.Add("@Desc", SqlDbType.NVarChar).Value = task.Desc ?? (object)DBNull.Value;
            cmdCenter.Parameters.Add("@Owner", SqlDbType.NVarChar).Value = task.Owner ?? (object)DBNull.Value;
            cmdCenter.Parameters.Add("@Benefit", SqlDbType.Float).Value = task.Benefit;
            cmdCenter.Parameters.Add("@DbCat", SqlDbType.NVarChar).Value = task.DbCat ?? (object)DBNull.Value;
            cmdCenter.Parameters.Add("@Tcd", SqlDbType.NVarChar).Value = task.Tcd ?? (object)DBNull.Value;
            cmdCenter.Parameters.Add("@AppLink", SqlDbType.NVarChar).Value = task.AppLink ?? (object)DBNull.Value;
            cmdCenter.Parameters.Add("@DataSource", SqlDbType.NVarChar).Value = task.DataSource ?? (object)DBNull.Value;
            cmdCenter.Parameters.Add("@Remark", SqlDbType.NVarChar).Value = task.Remark ?? (object)DBNull.Value;
            cmdCenter.Parameters.Add("@RemarkStatus", SqlDbType.NVarChar).Value = task.RemarkStatus ?? (object)DBNull.Value;
            cmdCenter.Parameters.Add("@Attachment", SqlDbType.NVarChar).Value = task.Attachment ?? (object)DBNull.Value;

            await cmdCenter.ExecuteNonQueryAsync();

            if (task.Stations != null)
            {
                foreach (var st in task.Stations)
                {
                    using var cmdSt = new SqlCommand("INSERT INTO TaskStation (TaskRegId, StationName, MpValue, UrlLink) VALUES (@RegId, @StationName, @MpValue, @UrlLink)", conn, transaction);
                    cmdSt.Parameters.AddWithValue("@RegId", newId);
                    cmdSt.Parameters.AddWithValue("@StationName", st.StationName ?? "");
                    cmdSt.Parameters.AddWithValue("@MpValue", st.MpValue ?? "");
                    cmdSt.Parameters.AddWithValue("@UrlLink", st.UrlLink ?? "");
                    await cmdSt.ExecuteNonQueryAsync();
                }
            }
        }
        transaction.Commit();

        // 匯入成功 (覆蓋整批資料) 後清空 Upload 資料夾，避免上一批的附件實體檔殘留累積。
        // 注意：Excel 只帶回 Attachment 路徑字串、不含實體檔，故清檔後匯入資料的附件連結會失效屬預期行為。
        ClearUploadDir();

        return Results.Ok(new { success = true });
    }
    catch (Exception ex)
    {
        transaction.Rollback();
        return Results.Problem(ex.Message);
    }
});

// 5. 刪除資料
app.MapDelete("/api/tasks/{id}", async (int id) =>
{
    using var conn = new SqlConnection(connStr);
    await conn.OpenAsync();

    // 先取出附件路徑，連同任務一起把實體檔案刪除，避免 Upload 殘留孤兒檔
    string attachPath = null;
    using (var cmdGet = new SqlCommand("SELECT Attachment FROM TaskCenter WHERE RegId=@RegId", conn))
    {
        cmdGet.Parameters.AddWithValue("@RegId", id);
        var val = await cmdGet.ExecuteScalarAsync();
        if (val != null && val != DBNull.Value) attachPath = val.ToString();
    }

    using var cmd = new SqlCommand("DELETE FROM TaskCenter WHERE RegId=@RegId", conn);
    cmd.Parameters.AddWithValue("@RegId", id);
    await cmd.ExecuteNonQueryAsync();

    DeleteUploadFile(attachPath);
    return Results.Ok(new { success = true });
});

// 6. 上傳附件檔案 (Excel / PPT / 圖檔)，存入 wwwroot/Upload 並回傳可存取路徑
app.MapPost("/api/upload", async (HttpRequest request) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest(new { error = "需以 multipart/form-data 上傳" });

    var form = await request.ReadFormAsync();
    var file = form.Files["file"];
    if (file == null || file.Length == 0)
        return Results.BadRequest(new { error = "未收到檔案" });

    var allowed = new[] { ".xlsx", ".xls", ".ppt", ".pptx", ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" };
    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (!allowed.Contains(ext))
        return Results.BadRequest(new { error = "不支援的檔案格式 (僅限 Excel / PPT / 圖檔)" });

    var uploadDir = GetUploadDir();

    // ⭐ 唯一檔名規則：GUID 確保每個檔案全域唯一，
    //    不同任務即使上傳同名檔也不會互相覆蓋或誤刪；後綴原檔名方便人工辨識。
    var safeName = Path.GetFileNameWithoutExtension(file.FileName);
    foreach (var c in Path.GetInvalidFileNameChars()) safeName = safeName.Replace(c, '_');
    if (safeName.Length > 60) safeName = safeName.Substring(0, 60);
    var uniqueName = $"{Guid.NewGuid():N}_{safeName}{ext}";

    using (var stream = File.Create(Path.Combine(uploadDir, uniqueName)))
        await file.CopyToAsync(stream);

    return Results.Ok(new { path = "/Upload/" + uniqueName, name = file.FileName });
});

// 7. 刪除附件檔案 (僅限 /Upload/ 底下，以檔名定位，防止路徑跳脫)
app.MapDelete("/api/upload", (string path) =>
{
    DeleteUploadFile(path);
    return Results.Ok(new { success = true });
});

// 取得 Upload 實體資料夾路徑 (不存在則建立)
string GetUploadDir()
{
    var root = app.Environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
    var dir = Path.Combine(root, "Upload");
    Directory.CreateDirectory(dir);
    return dir;
}

// 清空 Upload 資料夾內所有檔案 (匯入覆蓋資料後呼叫，避免舊附件殘留累積)。
// 採 best-effort：個別檔案被鎖住時略過，不中斷流程。
void ClearUploadDir()
{
    var dir = GetUploadDir();
    foreach (var f in Directory.GetFiles(dir))
    {
        try { File.Delete(f); } catch { /* 檔案被佔用則略過 */ }
    }
}

// 安全刪除附件：限定 /Upload/ 路徑、只用檔名定位，杜絕 ../ 路徑跳脫誤刪其他檔案
void DeleteUploadFile(string webPath)
{
    if (string.IsNullOrWhiteSpace(webPath)) return;
    if (!webPath.StartsWith("/Upload/", StringComparison.OrdinalIgnoreCase)) return;
    var full = Path.Combine(GetUploadDir(), Path.GetFileName(webPath));
    if (File.Exists(full)) File.Delete(full);
}

// 任務儲存時把附件實體檔改名為 {RegId}_{GUID}_原檔名，讓檔名能直接對應資料列；
// GUID 仍保留以防不同任務同名誤刪。上傳階段檔名為 {GUID}_原檔名 (尚不知 RegId)，
// 故在新增/編輯任務 (此時 RegId 已確定) 時才補上 RegId 前綴。
string ApplyRegIdToAttachment(int regId, string? webPath)
{
    if (string.IsNullOrWhiteSpace(webPath)) return "";
    if (!webPath.StartsWith("/Upload/", StringComparison.OrdinalIgnoreCase)) return webPath;

    var fileName = Path.GetFileName(webPath);
    // 去掉可能已存在的舊 {數字}_ (RegId) 前綴，避免重複堆疊；GUID 為 16 進位不會被誤判
    var stripped = System.Text.RegularExpressions.Regex.Replace(fileName, @"^\d+_", "");
    var desired = $"{regId}_{stripped}";
    if (desired == fileName) return webPath; // 已是正確檔名，免改

    var dir = GetUploadDir();
    var src = Path.Combine(dir, fileName);
    var dst = Path.Combine(dir, desired);
    try
    {
        if (File.Exists(src))
        {
            if (File.Exists(dst)) File.Delete(dst);
            File.Move(src, dst);
        }
    }
    catch { /* 改名失敗時退回原路徑，不阻斷儲存 */ return webPath; }

    return "/Upload/" + desired;
}

app.Run();

public class TaskDto
{
    public int RegId { get; set; }
    public string Date { get; set; }
    public string CompletionDate { get; set; }
    public string NotesId { get; set; }
    public string Status { get; set; }
    public string Dept { get; set; }
    public string Sec { get; set; }
    public string Applicant { get; set; }
    public string Desc { get; set; }
    public string Owner { get; set; }
    public double Benefit { get; set; }
    public string DbCat { get; set; }
    public string Tcd { get; set; }
    public string AppLink { get; set; }
    public string DataSource { get; set; }
    public string Remark { get; set; }
    public string RemarkStatus { get; set; }
    public string Attachment { get; set; }
    public List<StationDto> Stations { get; set; }
}

public class StationDto
{
    public string StationName { get; set; }
    public string MpValue { get; set; }
    public string UrlLink { get; set; }
}
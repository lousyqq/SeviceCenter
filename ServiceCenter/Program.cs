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
        SELECT t.RegId, t.RegDate, t.CompletionDate, t.Status, t.Department, t.Section, t.Applicant, t.Description, 
               t.Owner, t.Benefit, t.DBCat, t.TCD, t.AppLink, t.DataSource,
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
        using var cmd = new SqlCommand(@"
            INSERT INTO TaskCenter 
            (RegDate, CompletionDate, Status, Department, Section, Applicant, Description, Owner, Benefit, DBCat, TCD, AppLink, DataSource) 
            OUTPUT INSERTED.RegId 
            VALUES 
            (@RegDate, @CompletionDate, @Status, @Dept, @Sec, @Applicant, @Desc, @Owner, @Benefit, @DbCat, @Tcd, @AppLink, @DataSource)", conn, transaction);

        // 強制指定 SqlDbType 避免 NULL 轉型失敗
        cmd.Parameters.Add("@RegDate", SqlDbType.Date).Value = string.IsNullOrWhiteSpace(task.Date) ? DBNull.Value : task.Date;
        cmd.Parameters.Add("@CompletionDate", SqlDbType.Date).Value = string.IsNullOrWhiteSpace(task.CompletionDate) ? DBNull.Value : task.CompletionDate;
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

        int newId = (int)await cmd.ExecuteScalarAsync();

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
        using var cmd = new SqlCommand(@"
            UPDATE TaskCenter SET 
                RegDate=@RegDate, CompletionDate=@CompletionDate, Status=@Status, Department=@Dept, 
                Section=@Sec, Applicant=@Applicant, Description=@Desc, 
                Owner=@Owner, Benefit=@Benefit, DBCat=@DbCat, TCD=@Tcd, 
                AppLink=@AppLink, DataSource=@DataSource 
            WHERE RegId=@RegId", conn, transaction);

        cmd.Parameters.Add("@RegId", SqlDbType.Int).Value = id;
        cmd.Parameters.Add("@RegDate", SqlDbType.Date).Value = string.IsNullOrWhiteSpace(task.Date) ? DBNull.Value : task.Date;
        cmd.Parameters.Add("@CompletionDate", SqlDbType.Date).Value = string.IsNullOrWhiteSpace(task.CompletionDate) ? DBNull.Value : task.CompletionDate;
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

        // 重置識別碼
        using var cmdResetIdent = new SqlCommand("DBCC CHECKIDENT ('TaskCenter', RESEED, 0)", conn, transaction);
        await cmdResetIdent.ExecuteNonQueryAsync();

        foreach (var task in tasks)
        {
            using var cmdCenter = new SqlCommand(@"
                INSERT INTO TaskCenter 
                (RegDate, CompletionDate, Status, Department, Section, Applicant, Description, Owner, Benefit, DBCat, TCD, AppLink, DataSource) 
                OUTPUT INSERTED.RegId 
                VALUES 
                (@RegDate, @CompletionDate, @Status, @Dept, @Sec, @Applicant, @Desc, @Owner, @Benefit, @DbCat, @Tcd, @AppLink, @DataSource)", conn, transaction);

            cmdCenter.Parameters.Add("@RegDate", SqlDbType.Date).Value = string.IsNullOrWhiteSpace(task.Date) ? DBNull.Value : task.Date;
            cmdCenter.Parameters.Add("@CompletionDate", SqlDbType.Date).Value = string.IsNullOrWhiteSpace(task.CompletionDate) ? DBNull.Value : task.CompletionDate;
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

            int newId = (int)await cmdCenter.ExecuteScalarAsync();

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
    using var cmd = new SqlCommand("DELETE FROM TaskCenter WHERE RegId=@RegId", conn);
    cmd.Parameters.AddWithValue("@RegId", id);
    await cmd.ExecuteNonQueryAsync();
    return Results.Ok(new { success = true });
});

app.Run();

public class TaskDto
{
    public int RegId { get; set; }
    public string Date { get; set; }
    public string CompletionDate { get; set; }
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
    public List<StationDto> Stations { get; set; }
}

public class StationDto
{
    public string StationName { get; set; }
    public string MpValue { get; set; }
    public string UrlLink { get; set; }
}
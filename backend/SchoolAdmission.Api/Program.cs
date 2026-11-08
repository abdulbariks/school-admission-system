using System.Data;
using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Models;

var builder = WebApplication.CreateBuilder(args);
var connection = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Port=5432;Database=school_admission;Username=postgres;Password=postgres";
builder.Services.AddDbContext<AdmissionDbContext>(o => o.UseNpgsql(connection));
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddEndpointsApiExplorer();
var app = builder.Build();
app.UseCors();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AdmissionDbContext>();
    await db.Database.EnsureCreatedAsync();
}

var holdDuration = Math.Clamp(int.TryParse(Environment.GetEnvironmentVariable("HOLD_DURATION_SECONDS"), out var seconds) ? seconds : 600, 30, 86400);
static bool ValidStudent(string? id) => id is { Length: 17 } && id.All(char.IsDigit);
static IResult Error(int status, string message) => Results.Problem(statusCode: status, title: message);
static async Task ExpireAsync(AdmissionDbContext db, DateTime now, CancellationToken ct) =>
    await db.Holds.Where(h => h.Status == "HELD" && h.ExpiresAt <= now).ExecuteUpdateAsync(s => s.SetProperty(h => h.Status, "EXPIRED"), ct);
static object ClassDto(SchoolClass c, int held, int confirmed) => new { id = c.PublicId, name = c.Name, totalSeats = c.TotalSeats, heldSeats = held, confirmedSeats = confirmed, availableSeats = c.TotalSeats - held - confirmed };

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapPost("/classes", async (CreateClassRequest req, AdmissionDbContext db, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Name) || req.Name.Trim().Length > 120 || req.TotalSeats is < 1 or > 100000)
        return Error(400, "Enter a class name and a seat limit greater than zero.");
    var entity = new SchoolClass { Name = req.Name.Trim(), TotalSeats = req.TotalSeats };
    db.Classes.Add(entity);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/classes/{entity.PublicId}", new { id = entity.PublicId, name = entity.Name, totalSeats = entity.TotalSeats });
});

app.MapGet("/classes", async (AdmissionDbContext db, CancellationToken ct) =>
{
    var now = DateTime.UtcNow;
    await ExpireAsync(db, now, ct);
    var classes = await db.Classes.OrderBy(c => c.CreatedAt).ToListAsync(ct);
    var stats = await db.Holds.Where(h => h.Status == "HELD" && h.ExpiresAt > now || h.Status == "CONFIRMED")
        .GroupBy(h => new { h.ClassId, h.Status }).Select(g => new { g.Key.ClassId, g.Key.Status, Count = g.Count() }).ToListAsync(ct);
    return Results.Ok(classes.Select(c => ClassDto(c, stats.Where(s => s.ClassId == c.Id && s.Status == "HELD").Sum(s => s.Count), stats.Where(s => s.ClassId == c.Id && s.Status == "CONFIRMED").Sum(s => s.Count))));
});

app.MapGet("/classes/{classId}", async (string classId, AdmissionDbContext db, CancellationToken ct) =>
{
    var now = DateTime.UtcNow;
    await ExpireAsync(db, now, ct);
    var c = await db.Classes.SingleOrDefaultAsync(x => x.PublicId == classId, ct);
    if (c is null) return Error(404, "Class not found.");
    var held = await db.Holds.CountAsync(h => h.ClassId == c.Id && h.Status == "HELD" && h.ExpiresAt > now, ct);
    var confirmed = await db.Holds.CountAsync(h => h.ClassId == c.Id && h.Status == "CONFIRMED", ct);
    return Results.Ok(ClassDto(c, held, confirmed));
});

app.MapPost("/classes/{classId}/holds", async (string classId, HttpRequest request, AdmissionDbContext db, CancellationToken ct) =>
{
    var studentId = request.Headers["X-Student-Id"].FirstOrDefault();
    if (!ValidStudent(studentId)) return Error(400, "Student ID must contain exactly 17 digits.");
    await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
    var classRows = await db.Classes.Where(c => c.PublicId == classId).ToListAsync(ct);
    var schoolClass = classRows.SingleOrDefault();
    if (schoolClass is null) return Error(404, "Class not found.");
    // Lock the class row: every allocation for this class serializes across API processes.
    await db.Database.SqlQuery<int>($"SELECT 1 AS \"Value\" FROM \"Classes\" WHERE \"Id\" = {schoolClass.Id} FOR UPDATE").ToListAsync(ct);
    var now = DateTime.UtcNow;
    await ExpireAsync(db, now, ct);
    var duplicate = await db.Holds.AnyAsync(h => h.ClassId == schoolClass.Id && h.StudentId == studentId && (h.Status == "CONFIRMED" || h.Status == "HELD" && h.ExpiresAt > now), ct);
    if (duplicate) return Error(409, "You already have an active or confirmed application for this class.");
    var held = await db.Holds.CountAsync(h => h.ClassId == schoolClass.Id && h.Status == "HELD" && h.ExpiresAt > now, ct);
    var confirmed = await db.Holds.CountAsync(h => h.ClassId == schoolClass.Id && h.Status == "CONFIRMED", ct);
    if (held + confirmed >= schoolClass.TotalSeats) return Error(409, "Sorry, no seats are currently available.");
    var hold = new AdmissionHold { ClassId = schoolClass.Id, StudentId = studentId!, ExpiresAt = now.AddSeconds(holdDuration) };
    db.Holds.Add(hold);
    await db.SaveChangesAsync(ct);
    await tx.CommitAsync(ct);
    return Results.Created($"/holds/{hold.PublicId}", new { holdId = hold.PublicId, classId = schoolClass.PublicId, studentId, status = hold.Status, expiresAt = hold.ExpiresAt });
});

app.MapGet("/holds/{holdId}", async (string holdId, AdmissionDbContext db, CancellationToken ct) =>
{
    var hold = await db.Holds.Include(h => h.Class).SingleOrDefaultAsync(h => h.PublicId == holdId, ct);
    if (hold is null) return Error(404, "Application not found.");
    if (hold.Status == "HELD" && hold.ExpiresAt <= DateTime.UtcNow) { hold.Status = "EXPIRED"; await db.SaveChangesAsync(ct); }
    return Results.Ok(new { holdId = hold.PublicId, classId = hold.Class!.PublicId, status = hold.Status, expiresAt = hold.ExpiresAt });
});

app.MapPost("/holds/{holdId}/confirm", async (string holdId, HttpRequest request, AdmissionDbContext db, CancellationToken ct) =>
{
    var studentId = request.Headers["X-Student-Id"].FirstOrDefault();
    if (!ValidStudent(studentId)) return Error(400, "Student ID must contain exactly 17 digits.");
    await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
    var hold = await db.Holds.Include(h => h.Class).SingleOrDefaultAsync(h => h.PublicId == holdId, ct);
    if (hold is null) return Error(404, "Application not found.");
    await db.Database.SqlQuery<int>($"SELECT 1 AS \"Value\" FROM \"Classes\" WHERE \"Id\" = {hold.ClassId} FOR UPDATE").ToListAsync(ct);
    await db.Entry(hold).ReloadAsync(ct);
    if (hold.StudentId != studentId) return Error(403, "This application belongs to another student.");
    if (hold.Status == "CONFIRMED") { await tx.CommitAsync(ct); return Results.Ok(new { holdId, status = hold.Status }); }
    if (hold.Status != "HELD" || hold.ExpiresAt <= DateTime.UtcNow)
    {
        if (hold.Status == "HELD") { hold.Status = "EXPIRED"; await db.SaveChangesAsync(ct); }
        return Error(409, "This reservation has expired or is no longer active.");
    }
    hold.Status = "CONFIRMED"; hold.ConfirmedAt = DateTime.UtcNow;
    await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    return Results.Ok(new { holdId, status = hold.Status });
});

app.MapDelete("/holds/{holdId}", async (string holdId, HttpRequest request, AdmissionDbContext db, CancellationToken ct) =>
{
    var studentId = request.Headers["X-Student-Id"].FirstOrDefault();
    if (!ValidStudent(studentId)) return Error(400, "Student ID must contain exactly 17 digits.");
    var hold = await db.Holds.SingleOrDefaultAsync(h => h.PublicId == holdId, ct);
    if (hold is null) return Error(404, "Application not found.");
    if (hold.StudentId != studentId) return Error(403, "This application belongs to another student.");
    if (hold.Status == "HELD") { hold.Status = "CANCELLED"; await db.SaveChangesAsync(ct); }
    return Results.NoContent();
});

app.MapGet("/me/holds", async (HttpRequest request, AdmissionDbContext db, CancellationToken ct) =>
{
    var studentId = request.Headers["X-Student-Id"].FirstOrDefault();
    if (!ValidStudent(studentId)) return Error(400, "Student ID must contain exactly 17 digits.");
    await ExpireAsync(db, DateTime.UtcNow, ct);
    var holds = await db.Holds.Include(h => h.Class).Where(h => h.StudentId == studentId).OrderByDescending(h => h.CreatedAt).ToListAsync(ct);
    return Results.Ok(holds.Select(h => new { holdId = h.PublicId, classId = h.Class!.PublicId, className = h.Class.Name, status = h.Status, expiresAt = h.ExpiresAt }));
});

app.Run();

public sealed record CreateClassRequest(string Name, int TotalSeats);
public partial class Program { }

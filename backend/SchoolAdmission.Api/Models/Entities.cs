namespace SchoolAdmission.Api.Models;

public sealed class SchoolClass
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PublicId { get; set; } = $"c_{Guid.NewGuid():N}";
    public string Name { get; set; } = "";
    public int TotalSeats { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<AdmissionHold> Holds { get; set; } = [];
}

public sealed class AdmissionHold
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PublicId { get; set; } = $"h_{Guid.NewGuid():N}";
    public Guid ClassId { get; set; }
    public SchoolClass? Class { get; set; }
    public string StudentId { get; set; } = "";
    public string Status { get; set; } = "HELD";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
}

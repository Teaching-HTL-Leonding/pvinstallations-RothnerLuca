using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<PvDbContext>(options => 
    options.UseSqlServer(builder.Configuration["ConnectionStrings:DefaultConnection"])); // ConnectionString stored appsettings.json
var app = builder.Build();

app.MapPost("/installations", async (PvDbContext context, AddPvInstallationDto newPvInstallation) => 
{
    if (newPvInstallation.Longitude < -180 || newPvInstallation.Longitude > 180) return Results.BadRequest("Longitude must be between -180 and 180");
    if (newPvInstallation.Latitude < -90 || newPvInstallation.Latitude > 90) return Results.BadRequest("Latitude must be between -90 and 90");
    if (newPvInstallation.Address.Length > 1024) return Results.BadRequest("Address must be less than 1024 characters");
    if (newPvInstallation.OwnerName.Length > 512) return Results.BadRequest("OwnerName must be less than 512 characters");
    if (newPvInstallation.Comments != null && newPvInstallation.Comments.Length > 1024) return Results.BadRequest("Comments must be less than 1024 characters");

    var dbPvInstallation = new PvInstallation 
    {
        Longitude = newPvInstallation.Longitude,
        Latitude = newPvInstallation.Latitude,
        Address = newPvInstallation.Address,
        OwnerName = newPvInstallation.OwnerName,
        IsActive = true,
        Comments = newPvInstallation.Comments
    };
    await context.PvInstallations.AddAsync(dbPvInstallation);

    // logging

    await context.SaveChangesAsync();
    return Results.Created($"/installations/{dbPvInstallation.ID}", dbPvInstallation.ID);
});

app.MapPost("/installations/{id}/deactivate", async (PvDbContext context, int id) => 
{
    var pvInstallation = await context.PvInstallations.FindAsync(id);
    if (pvInstallation == null) return Results.NotFound();
    pvInstallation.IsActive = false;

    // logging

    await context.SaveChangesAsync();
    return Results.Ok();
});

app.MapPost("/installations/{id}/reports", async (PvDbContext context, int id, AddProductionReportDto newProductionReport) => 
{
    var pvInstallation = await context.PvInstallations.FindAsync(id);
    if (pvInstallation == null) return Results.NotFound();

    if (newProductionReport.ProducedWattage < 0) return Results.BadRequest("ProducedWattage must be greater than 0");
    if (newProductionReport.HouseholdWattage < 0) return Results.BadRequest("HouseholdWattage must be greater than 0");
    if (newProductionReport.BatteryWattage < 0) return Results.BadRequest("BatteryWattage must be greater than 0");
    if (newProductionReport.GridWattage < 0) return Results.BadRequest("GridWattage must be greater than 0");

    DateTime currentUtcTime = DateTime.UtcNow;
    DateTime truncatedTime = currentUtcTime.AddSeconds(-currentUtcTime.Second).AddMilliseconds(-currentUtcTime.Millisecond);

    var dbProductionReport = new ProductionReport 
    {
        Timestamp = truncatedTime,
        ProducedWattage = newProductionReport.ProducedWattage,
        HouseholdWattage = newProductionReport.HouseholdWattage,
        BatteryWattage = newProductionReport.BatteryWattage,
        GridWattage = newProductionReport.GridWattage,
        PvInstallation = pvInstallation
    };
    await context.ProductionReports.AddAsync(dbProductionReport);
    await context.SaveChangesAsync();
    return Results.Created($"/installations/{id}/reports/{dbProductionReport.ID}", dbProductionReport.ID);
});

app.MapGet("/installations/{id}/reports", async (PvDbContext context, int id, DateTime? timestamp, int? duration) => 
{
    var pvInstallation = await context.PvInstallations.FindAsync(id);
    if (pvInstallation == null) return Results.NotFound();

    if (duration < 0) return Results.BadRequest("Duration must be greater than 0");

    var productionReportsList = await context.ProductionReports
        .Where(pr => pr.PvInstallationID == id)
        .Where(pr => timestamp == null || pr.Timestamp >= timestamp)
        .Where(pr => duration == null || pr.Timestamp <= timestamp.Value.AddMinutes(duration.Value))
        .ToListAsync();

    float producedWattageSum = 0;
    productionReportsList.ForEach(pr => producedWattageSum += pr.ProducedWattage);

    return Results.Ok(producedWattageSum * duration);
});

app.MapGet("/installations/{id}/timeline", async (PvDbContext context, int id, DateTime? startTimestamp, int? duration, int? page) =>
{
    var pvInstallation = await context.PvInstallations.FindAsync(id);
    if (pvInstallation == null) return Results.NotFound();

    if (duration < 0) return Results.BadRequest("Duration must be greater than 0");

    int pageSize = 60;
    if (page < 0) return Results.BadRequest("Page must be greater than 0");

    var productionReportsList = await context.ProductionReports
        .Where(pr => pr.PvInstallationID == id)
        .Where(pr => startTimestamp == null || pr.Timestamp >= startTimestamp)
        .Where(pr => duration == null || pr.Timestamp <= startTimestamp.Value.AddMinutes(duration.Value))
        .ToListAsync();

    if (productionReportsList.Count == 0) return Results.Ok(new ProducedReportTimelineDto(0, 0, 0, 0));

    List<ProducedReportTimelineDto> producedReportTimelineList = new();
    productionReportsList.ForEach(pr => {
        producedReportTimelineList.Add(new ProducedReportTimelineDto(pr.ProducedWattage, pr.HouseholdWattage, pr.BatteryWattage, pr.GridWattage));
    });

    int skip = pageSize * ((int)page! - 1);
    if (producedReportTimelineList.Count < skip) 
    {
        producedReportTimelineList = new();
    } else {
        producedReportTimelineList.Skip(skip).Take(pageSize);
    }

    return Results.Ok(producedReportTimelineList);
});

app.Run();

record AddPvInstallationDto(float Longitude, float Latitude, string Address, string OwnerName, string? Comments);
record AddProductionReportDto(float ProducedWattage, float HouseholdWattage, float BatteryWattage, float GridWattage);
record ProducedReportTimelineDto(float ProducedWattage, float HouseholdWattage, float BatteryWattage, float GridWattage);

class PvInstallation 
{
    public int ID { get; set; }
    public float Longitude { get; set; }
    public float Latitude { get; set; }
    [MaxLength(1024)]
    public string Address { get; set; } = "";
    [MaxLength(512)]
    public string OwnerName { get; set; } = "";
    public bool IsActive { get; set; }
    [MaxLength(1024)]
    public string? Comments { get; set; } = "";
}

class ProductionReport
{
    public int ID { get; set; }
    public DateTime Timestamp { get; set;}
    public float ProducedWattage { get; set; }
    public float HouseholdWattage { get; set; }
    public float BatteryWattage { get; set; }
    public float GridWattage { get; set; }
    public PvInstallation? PvInstallation { get; set; }
    public int? PvInstallationID { get; set; }
}

class InstallationLog
{
    public int ID { get; set; }
    public DateTime Timestamp { get; set; }
    public string Action { get; set; } = "";
    public string? PreviousValue { get; set; } 
    public string? NewValue { get; set; }
}

class PvDbContext : DbContext
{
    public PvDbContext(DbContextOptions<PvDbContext> options) : base(options) {}
    public DbSet<PvInstallation> PvInstallations => Set<PvInstallation>();
    public DbSet<ProductionReport> ProductionReports => Set<ProductionReport>();
    public DbSet<InstallationLog> InstallationLogs => Set<InstallationLog>();
}
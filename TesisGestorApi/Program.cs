using Microsoft.EntityFrameworkCore;
using TesisGestorApi.Data;
using TesisGestorApi.Interfaces;
using TesisGestorApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddDbContext<ApplicationDbContext>(opciones =>
    opciones.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IAsistenciaService, AsistenciaService>();
builder.Services.AddScoped<IAsistenciaUmbralService, AsistenciaUmbralService>();
builder.Services.AddScoped<IScannerService, ScannerService>();


builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IQrCredentialGenerationService, QrCredentialGenerationService>();
builder.Services.AddScoped<IQrCredentialDeliveryService, QrCredentialDeliveryService>();
builder.Services.AddScoped<IQrCredentialVisualService, QrCredentialVisualService>();
builder.Services.AddScoped<IQrCredentialEmailTemplateService, QrCredentialEmailTemplateService>();

builder.Services.AddSingleton<QrCredentialGenerationProgressStore>();
builder.Services.AddSingleton<QrCredentialDeliveryProgressStore>();

builder.Services.AddHostedService<AsistenciaUmbralEmailWorker>();

// Controllers
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Migraciones automáticas fuera de Development
if (!app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.Migrate();
    }
}

// Swagger
app.UseSwagger();
app.UseSwaggerUI();

// Middleware
app.UseCors();
app.UseAuthorization();

app.MapControllers();

app.Run();
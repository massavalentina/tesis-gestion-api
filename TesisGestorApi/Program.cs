using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using TesisGestorApi.Data;
using TesisGestorApi.Interfaces;
using TesisGestorApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddDbContext<ApplicationDbContext>(opciones =>
    opciones.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IParteDiarioService, ParteDiarioService>();
builder.Services.AddScoped<IAsistenciaService, AsistenciaService>();
builder.Services.AddScoped<IAsistenciaUmbralService, AsistenciaUmbralService>();
builder.Services.AddScoped<IScannerService, ScannerService>();
builder.Services.AddScoped<IRetiroService, RetiroService>();

builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IQrCredentialGenerationService, QrCredentialGenerationService>();
builder.Services.AddScoped<IQrCredentialDeliveryService, QrCredentialDeliveryService>();
builder.Services.AddScoped<IQrCredentialVisualService, QrCredentialVisualService>();
builder.Services.AddScoped<IQrCredentialEmailTemplateService, QrCredentialEmailTemplateService>();

builder.Services.AddSingleton<QrCredentialGenerationProgressStore>();
builder.Services.AddSingleton<QrCredentialDeliveryProgressStore>();

builder.Services.AddScoped<IUsuariosRolesService, UsuariosRolesService>();

// Auth
builder.Services.AddScoped<ITokenService, TokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Emisor"],
            ValidAudience = builder.Configuration["Jwt:Audiencia"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Clave"]!))
        };
    });

// JWT Authentication
var jwtSection = builder.Configuration.GetSection("Jwt");
var secretKey  = Encoding.UTF8.GetBytes(jwtSection["SecretKey"]!);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(secretKey),
            ValidateIssuer           = true,
            ValidIssuer              = jwtSection["Issuer"],
            ValidateAudience         = true,
            ValidAudience            = jwtSection["Audience"],
            ClockSkew                = TimeSpan.Zero,
        };
    });

// Controllers
builder.Services.AddControllers();

// Swagger con soporte Bearer
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Ejemplo: 'Bearer {token}'",
        Name        = "Authorization",
        In          = ParameterLocation.Header,
        Type        = SecuritySchemeType.ApiKey,
        Scheme      = "Bearer",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer",
                },
            },
            Array.Empty<string>()
        }
    });
});

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

// Migraciones automáticas y seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
    await DbSeeder.SeedAdminAsync(db);
    if (app.Environment.IsDevelopment())
        await DbSeeder.SeedDevUsuariosAsync(db);
}

// Swagger
app.UseSwagger();
app.UseSwaggerUI();

// Middleware
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

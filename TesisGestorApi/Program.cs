using Microsoft.EntityFrameworkCore;
using TesisGestorApi.Data;

var builder = WebApplication.CreateBuilder(args);


//Services


builder.Services.AddDbContext<ApplicationDbContext>(opciones =>
    opciones.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));


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

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS (abierto por ahora, simple)
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

// Swagger (en cualquier entorno, para pruebas)
app.UseSwagger();
app.UseSwaggerUI();

// Middleware
app.UseCors();
app.UseAuthorization();

app.MapControllers();

app.Run();

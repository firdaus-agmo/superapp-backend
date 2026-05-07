using Microsoft.EntityFrameworkCore;
using SuperAppBackend;

var builder = WebApplication.CreateBuilder(args);

// 1. Register the CORS service
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader());
});

// 2. Register the In-Memory Database
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase("SuperAppDb"));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

// 3. Apply the CORS policy
app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

app.Run();
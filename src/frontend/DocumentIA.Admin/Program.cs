using DocumentIA.Admin.Components;
using DocumentIA.Admin.Services;
using DocumentIA.Data.Context;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var connectionString = builder.Configuration["ConnectionStrings:DocumentIA"]
    ?? builder.Configuration["SqlConnectionString"];

if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<DocumentIADbContext>(options =>
        options.UseSqlServer(connectionString));
}

builder.Services.AddScoped<TipologiaAdminService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

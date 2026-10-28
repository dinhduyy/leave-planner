using LeavePlanner.Configuration;
using LeavePlanner.Domain.Enums;
using LeavePlanner.Errors;
using LeavePlanner.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var priorityJsonConverter = new JsonStringEnumConverter<PlanningPriority>(
    JsonNamingPolicy.CamelCase, allowIntegerValues: false);

builder.Services.AddControllers(options =>
    options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider()))
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(priorityJsonConverter));
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
    options.SerializerOptions.Converters.Add(priorityJsonConverter));
builder.Services.AddOpenApi(options => options.AddSchemaTransformer((schema, context, _) =>
{
    if (context.JsonTypeInfo.Type == typeof(PlanningPriority) ||
        context.JsonTypeInfo.Type == typeof(PlanningPriority?))
    {
        schema.Type = "string";
    }

    return Task.CompletedTask;
}));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddHolidayInfrastructure(builder.Configuration);
builder.Services.AddLeavePlanning();

var app = builder.Build();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStatusCodePages();
app.MapControllers();
app.Run();

public partial class Program;

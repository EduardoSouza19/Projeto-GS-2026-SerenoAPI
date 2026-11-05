using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Context;
using SerenoAPI.Data;
using SerenoAPI.Models;
using SerenoAPI.Repositories;
using SerenoAPI.Services;
using SerenoAPI.Settings;
using SerenoAPI.Telemetry;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

//  Logs (Serilog) 
builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .MinimumLevel.Information()
        .WriteTo.Console()
        .WriteTo.File(path: "logs/log-.txt", rollingInterval: RollingInterval.Day)
        .Enrich.FromLogContext();
});

//  Observabilidade (OpenTelemetry) 
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("SerenoAPI")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(AppMetrics.Meter.Name)
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter());

builder.Services.AddScoped<TelemetryActionFilter>();

//  Controllers + JSON do Contrato + erro de validação padronizado 
builder.Services
    .AddControllers(options => options.Filters.AddService<TelemetryActionFilter>())
    .AddJsonOptions(options => JsonPadrao.Configurar(options.JsonSerializerOptions))
    .ConfigureApiBehaviorOptions(options =>
    {
        // Qualquer dado inválido vira: { status: 400, erro: "VALIDACAO", campos: [...] }
        options.InvalidModelStateResponseFactory = context =>
        {
            var campos = context.ModelState
                .Where(item => item.Value!.Errors.Count > 0)
                .SelectMany(item => item.Value!.Errors.Select(erro => new CampoErro
                {
                    Campo = NomeDoCampo(item.Key),
                    Mensagem = erro.ErrorMessage.Contains("JSON") || string.IsNullOrWhiteSpace(erro.ErrorMessage)
                        ? "valor inválido"
                        : erro.ErrorMessage
                }))
                .ToList();

            return new BadRequestObjectResult(ErroResponse.Criar(
                StatusCodes.Status400BadRequest, "VALIDACAO", "Dados inválidos", context.HttpContext.Request.Path, campos));
        };
    });

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

//  Swagger (OpenAPI) 
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));

    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Description = "Cole o token gerado em POST /api/auth/token (ou o token do login do app)."
    });

    options.AddSecurityRequirement(document =>
        new Microsoft.OpenApi.OpenApiSecurityRequirement
        {
            [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
        });
});

//  Oracle (EF Core) 
var connectionString = builder.Configuration.GetConnectionString("Default");

builder.Services.AddDbContext<AppDbContext>(options => options.UseOracle(connectionString));

builder.Services.AddScoped<StatusService>();
builder.Services.AddScoped<IdempotencyService>();

//  MongoDB 
builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDbSettings"));
builder.Services.AddSingleton<MongoHistoricoRepository>();

//  ViaCEP (serviço externo) 
builder.Services.AddHttpClient<ViaCepService>(client =>
{
    client.BaseAddress = new Uri("https://viacep.com.br/");
    client.Timeout = TimeSpan.FromSeconds(5);
});

//  Health Checks: Oracle + MongoDB 
var healthChecks = builder.Services.AddHealthChecks()
    .AddCheck<MongoHealthCheck>("mongodb", tags: new[] { "db" });

if (string.IsNullOrWhiteSpace(connectionString))
{
    healthChecks.AddCheck("oracle",
        () => HealthCheckResult.Unhealthy("ConnectionStrings:Default não configurada."),
        tags: new[] { "db" });
}
else
{
    healthChecks.AddOracle(
        connectionString: connectionString,
        name: "oracle",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "db" });
}

//  JWT
var jwtSecret = builder.Configuration["JWT_SECRET"]
    ?? throw new InvalidOperationException("Configure a variável JWT_SECRET (mínimo 32 caracteres).");

builder.Services.AddSingleton<TokenService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // mantém "sub", "email" e "role" com os nomes do Contrato

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "sereno-auth",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "sereno-app",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = "sub",
            RoleClaimType = "role"
        };

        // 401 e 403 
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(ErroResponse.Criar(401, "NAO_AUTENTICADO",
                    "Token ausente, inválido ou expirado. Faça login novamente.", context.Request.Path), JsonPadrao.Opcoes);
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(ErroResponse.Criar(403, "ACESSO_NEGADO",
                    "Esta ação é permitida somente para GESTOR.", context.Request.Path), JsonPadrao.Opcoes);
            }
        };
    });

builder.Services.AddAuthorization(options =>
    options.AddPolicy("Gestor", policy => policy.RequireRole("GESTOR")));

//  Rate Limiting (HTTP 429 + Retry-After) 
var limiteGeral = builder.Configuration.GetValue("RateLimit:GeralPorMinuto", 100);
var limiteReportes = builder.Configuration.GetValue("RateLimit:ReportesPorMinuto", 10);

// Conta por usuário (claim "sub"); sem login, conta por IP
string ChaveDoUsuario(HttpContext http) =>
    http.User.FindFirst("sub")?.Value ?? http.Connection.RemoteIpAddress?.ToString() ?? "anonimo";

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, token) =>
    {
        var segundos = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? (int)Math.Ceiling(retryAfter.TotalSeconds)
            : 60;

        context.HttpContext.Response.Headers.RetryAfter = segundos.ToString();
        await context.HttpContext.Response.WriteAsJsonAsync(ErroResponse.Criar(429, "LIMITE_REQUISICOES",
            $"Muitas tentativas. Aguarde {segundos} segundos.", context.HttpContext.Request.Path), JsonPadrao.Opcoes, token);
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
        RateLimitPartition.GetFixedWindowLimiter("geral:" + ChaveDoUsuario(http),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = limiteGeral, Window = TimeSpan.FromMinutes(1) }));

    options.AddPolicy("reportes", http =>
        RateLimitPartition.GetFixedWindowLimiter(ChaveDoUsuario(http),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = limiteReportes, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

//  Erro 500
app.UseExceptionHandler(erro => erro.Run(async context =>
{
    var excecao = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    Log.Error(excecao, "Erro não tratado");

    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(ErroResponse.Criar(500, "ERRO_INTERNO",
        "Erro interno no servidor. Tente novamente em instantes.", context.Request.Path), JsonPadrao.Opcoes);
}));

app.Use(async (context, next) =>
{
    var activity = Activity.Current;

    using (LogContext.PushProperty("TraceId", activity?.TraceId.ToString()))
    using (LogContext.PushProperty("SpanId", activity?.SpanId.ToString()))
    {
        await next();
    }
});

app.UseSerilogRequestLogging();

app.UseSwagger();
app.UseSwaggerUI();

app.MapScalarApiReference(options =>
{
    options
        .WithTitle("Sereno API - Elevador Funciona?")
        .WithTheme(ScalarTheme.DeepSpace)
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);

    options.OpenApiRoutePattern = "/swagger/v1/swagger.json";
});

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";

        var result = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description
            })
        };

        await context.Response.WriteAsJsonAsync(result);
    }
});

// Cria as tabelas (se preciso) e cadastra as 5 estações de exemplo
await DataSeeder.SeedAsync(app.Services);

app.Run();

// "$.status" ou "Status" vira "status" (camelCase, igual ao JSON)
static string NomeDoCampo(string chave)
{
    var nome = chave.Replace("$.", "").Split('.').Last();
    return string.IsNullOrEmpty(nome) ? "corpo" : char.ToLowerInvariant(nome[0]) + nome[1..];
}

public partial class Program
{
}

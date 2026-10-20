using DeduplicacaoCSharp.Services;

var builder = WebApplication.CreateBuilder(args);

// A aplicação escuta somente no computador local.
builder.WebHost.UseUrls("http://127.0.0.1:5080");
builder.Services.AddRazorPages();
builder.Services.AddSingleton<ProjectStore>();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.Run();

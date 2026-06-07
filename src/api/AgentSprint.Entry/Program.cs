using Air.Cloud.WebApp.App;

var builder = WebApplication.CreateBuilder(args);

var app = builder.WebInjectInFile();
app.Run();


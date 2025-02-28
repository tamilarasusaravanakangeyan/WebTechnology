
namespace SecureCookieWebApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.WithOrigins("http://localhost:4200") // Angular app URL
                          .AllowCredentials()
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                });
            });

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddAuthentication("CookieAuth")
                            .AddCookie("CookieAuth", options =>
                            {
                                options.Cookie.HttpOnly = true; // Prevent JavaScript access
                                options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Use cookies only over HTTPS
                                options.Cookie.SameSite = SameSiteMode.Strict; // Protect against cross-site attacks
                                options.Cookie.Name = "AuthCookie";
                                options.ExpireTimeSpan = TimeSpan.FromMinutes(30); // Set expiration time
                                options.LoginPath = "/api/Auth/Login"; // Redirect unauthorized users
                            });
            builder.Services.AddAuthorization();
            builder.Services.AddControllers();
            var app = builder.Build();
            app.UseCors();
            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();


            app.MapControllers();

            app.Run();


        }
    }
}

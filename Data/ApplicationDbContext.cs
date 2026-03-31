   // Data/ApplicationDbContext.cs
   using Microsoft.EntityFrameworkCore;
   using main_project.Models;

   namespace main_project.Data
   {
       public class ApplicationDbContext : DbContext
       {
           public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

           public DbSet<Survey> Surveys { get; set; } // Добавьте DbSet для вашей модели
       }
       
   }
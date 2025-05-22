using Microsoft.EntityFrameworkCore;
using SmartRouting.Models;
// Potentially add: using NetTopologySuite.Geometries;

namespace SmartRouting.Configurations
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Address> Addresses { get; set; } = null!;
        public DbSet<IndexDistance> IndexDistances { get; set; } = null!;
        // Add other DbSets as needed for your models
        // public DbSet<Vehicle> Vehicles { get; set; } = null!;
        // public DbSet<Depot> Depots { get; set; } = null!;
        // public DbSet<DeliveryOrder> DeliveryOrders { get; set; } = null!;
        // public DbSet<PickupOrder> PickupOrders { get; set; } = null!;


        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Fallback configuration if not configured in Program.cs, for example during design-time operations.
                // Consider moving the connection string to appsettings.json or environment variables.
                // optionsBuilder.UseSqlServer("Name=ConnectionStrings:DefaultConnection",
                //    x => x.UseNetTopologySuite()); // Added UseNetTopologySuite
            }
            // Ensure NetTopologySuite is used if options are configured elsewhere (e.g. Program.cs)
            // This might be redundant if UseNetTopologySuite() is already called during AddDbContext in Program.cs
            // but ensures it for design-time tools if they don't pick up Program.cs configuration.
            // However, the primary place to configure this is in Program.cs with AddDbContext.
            // For EF Core tools to work correctly, especially with migrations, ensure this is configured.
            // If you have `optionsBuilder.UseSqlServer(connectionString)` in Program.cs, modify it there to include UseNetTopologySuite.
            // For now, we assume it will be configured in Program.cs or this OnConfiguring will be hit by tools.
            // A common pattern is to have the UseSqlServer call here if not configured, and ensure it includes UseNetTopologySuite.
            // Example for design-time tools if not configured by host:
            // if (!optionsBuilder.IsConfigured)
            // {
            //     IConfigurationRoot configuration = new ConfigurationBuilder()
            //        .SetBasePath(Directory.GetCurrentDirectory())
            //        .AddJsonFile("appsettings.json")
            //        .Build();
            //     var connectionString = configuration.GetConnectionString("DefaultConnection");
            //     optionsBuilder.UseSqlServer(connectionString, sqlServerOptionsAction: sqlOptions =>
            //     {
            //         sqlOptions.UseNetTopologySuite();
            //     });
            // }
        }


		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			// Address Configuration
            modelBuilder.Entity<Address>(entity =>
            {
                entity.ToTable("tbl_001");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.Property(e => e.Street).HasMaxLength(255);
                // Other properties like Name, Phone, District, Province, Ward, Address1 are configured via attributes in the Address model.

                entity.Property(e => e.Location)
                    .HasColumnType("geography");

                // Spatial index will be added manually in the migration file.
            });

            // IndexDistance Configuration
            modelBuilder.Entity<IndexDistance>(entity =>
			{
				entity.ToTable("tbl_002"); 
				entity.HasKey(d => d.Id); // Set Id as the primary key
				entity.Property(d => d.Id).ValueGeneratedOnAdd(); // Configure Id to be auto-generated

				// Add a unique constraint for the combination of Loc1 and Loc2
				entity.HasIndex(d => new { d.Loc1, d.Loc2 }).IsUnique();

				// entity.HasOne<Address>() 
				// 	.WithMany(a => a.IndexDistancesAsLoc1) 
				// 	.HasForeignKey(d => d.Loc1)
				// 	.OnDelete(DeleteBehavior.Restrict); 

				// entity.HasOne<Address>() 
				// 	.WithMany(a => a.IndexDistancesAsLoc2) 
				// 	.HasForeignKey(d => d.Loc2)
				// 	.OnDelete(DeleteBehavior.Restrict); 
			});

			// Exclude other entities from being mapped to the database
            modelBuilder.Ignore<DeliveryOrder>();
            modelBuilder.Ignore<PickupOrder>();
            modelBuilder.Ignore<Vehicle>();
            modelBuilder.Ignore<Depot>();

			// If you have DataSeeder
			// new DataSeeder(modelBuilder).Seed();
		}
    }
}
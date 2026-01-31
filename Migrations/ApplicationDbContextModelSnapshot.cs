using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ljp_itsolutions.Data;

#nullable disable

namespace ljp_itsolutions.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    partial class ApplicationDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.0");

            modelBuilder.Entity("ljp_itsolutions.Models.User", b =>
            {
                b.Property<Guid>("Id");

                b.Property<string>("FullName")
                    .IsRequired();

                b.Property<bool>("IsArchived");

                b.Property<string>("Password")
                    .IsRequired();

                b.Property<int>("Role");

                b.Property<string>("Username")
                    .IsRequired();

                b.HasKey("Id");

                b.ToTable("Users");
            });
        }
    }
}
